using Discord;
using Discord.Audio;
using Discord.WebSocket;
using DiscordBot.Extensions;
using DiscordBot.Models;
using DiscordBot.Modules.Interfaces;
using DiscordBot.Repositories.Interfaces;
using DiscordBot.Services.Interfaces;
using System.Collections.Concurrent;
using System.Reflection;

namespace DiscordBot.Services
{
    public class MusicService : IMusicService
    {
        private DiscordSocketClient _client;

        private IConfigurationRepository _configurationRepository;
        private IYtDlp _ytDlp;
        private IFFmpeg _ffmpeg;

        private ConcurrentDictionary<ulong, GuildData> _guildDataDict;

        // ToDo:
        // - Tidy up embed creation. Current setup makes me cringe
        // - Revisit StopPlayingAsync(), ClearQueueAsync() & SkipSongAsync() as the code is very repetive
        // - Add support for playlists?

        public MusicService(DiscordSocketClient client, IConfigurationRepository configurationRepository, IYtDlp ytDlp, IFFmpeg ffmpeg)
        {
            _client = client ?? throw new NullReferenceException(nameof(client));

            _configurationRepository = configurationRepository ?? throw new NullReferenceException(nameof(configurationRepository));
            _ytDlp = ytDlp ?? throw new NullReferenceException(nameof(ytDlp));
            _ffmpeg = ffmpeg ?? throw new NullReferenceException(nameof(ffmpeg));

            _guildDataDict = new();
        }

        public async Task PlayAsync(SocketSlashCommand command)
        {
            try
            {
                if (command.User is not IGuildUser user || command.GuildId is not ulong guildId)
                {
                    throw new Exception($"> [ERROR]: SocketSlashCommand.User was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                }

                // Try finding an already existing guild in the dictionary
                GuildData guildData = _guildDataDict.TryGetValue(guildId, out var foundGuild) ? foundGuild : new GuildData();

                IVoiceChannel voiceChannel = user.VoiceChannel;

                if (voiceChannel != null)
                {
                    // Execution order and logic:
                    // 1. Make sure channel has room for the bot
                    // 2. Check if we're already connected to a channel. Skip to next step if we have a valid IAudioClient
                    // 3. Connect to the channel and create an IAudioClient. Throw an error if there was an issue

                    if (voiceChannel.UserLimit <= voiceChannel.GetUsersAsync().CountAsync().Result)
                    {
                        // Send ephemeral response informing about channel being full
                        await SendErrorResponseAsync("channel-full", command);

                        return;
                    }
                    else if (guildData.AudioClient != null && guildData.AudioClient.ConnectionState == ConnectionState.Connected)
                    {
                        // Tell discord we acknowledge the interaction
                        await command.DeferAsync();

                        // Add songs to queue
                        await AppendQueryToQueueAsync(command);

                        // Stream audio
                        await StreamAudio(command);

                        return;
                    }
                    else if (guildData.AudioClient == null || guildData.AudioClient.ConnectionState == ConnectionState.Disconnected)
                    {
                        guildData.AudioClient = await user.VoiceChannel.ConnectAsync(false, false, false, false);

                        // Check if we we're able to connect. Throw error if not
                        if (guildData.AudioClient == null || guildData.AudioClient.ConnectionState == ConnectionState.Disconnected)
                        {
                            throw new Exception($"Unable to create IAudioClient with IVoiceChannel.ConnectAsync in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                        }

                        // Update _guildData with guildData object
                        _guildDataDict.AddOrUpdate(guildId, guildData, (key, oldValue) => oldValue);

                        // Tell discord we acknowledge the interaction
                        await command.DeferAsync();

                        // Add songs to queue
                        await AppendQueryToQueueAsync(command);

                        // Stream audio
                        await StreamAudio(command);
                    }

                }
                else
                {
                    // Inform user with an ephemeral response that they should be in a voice channel before requesting /play
                    await SendErrorResponseAsync("channel-not-found", command);

                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message != null ? $"> [ERROR]: {ex.Message}" : $"> [ERROR]: Something went wrong in {this.GetType().Name} : PlayAsync()");

                await SendErrorResponseAsync("error", command);
            }

            return;
        }

        public async Task StopPlayingAsync(ulong guildId, SocketSlashCommand? command = null, SocketMessageComponent? component = null)
        {
            try
            {
                // Get the valid object from parameters
                var validObject = GetValidInteractionObject(command, component).Result;

                switch (validObject)
                {
                    case SocketSlashCommand:
                        if (validObject is SocketSlashCommand validCommand && validCommand.User is SocketGuildUser commandUser)
                        {
                            if (commandUser.VoiceChannel == null || !commandUser.VoiceChannel.Users.Any(u => u.Id == _client.CurrentUser.Id))
                            {
                                await SendErrorResponseAsync("stop-playing-wrong-channel", validCommand);

                                return;
                            }

                            await validCommand.DeferAsync();

                            await commandUser.VoiceChannel.DisconnectAsync();

                            await SendResponseAsync(guildId, "stopped-playing", validCommand);

                            return;
                        }
                        break;
                    case SocketMessageComponent:
                        if (validObject is SocketMessageComponent validComponent && validComponent.User is SocketGuildUser componentUser)
                        {
                            if (componentUser.VoiceChannel == null || !componentUser.VoiceChannel.Users.Any(u => u.Id == _client.CurrentUser.Id))
                            {
                                await SendErrorResponseAsync("stop-playing-wrong-channel", null, validComponent);

                                return;
                            }

                            await validComponent.DeferAsync();

                            await componentUser.VoiceChannel.DisconnectAsync();

                            await SendResponseAsync(guildId, "stopped-playing", null, validComponent);

                            return;
                        }
                        break;
                }

                // Disable the buttons of the last "now-playing" reply
                await DisableButtons(guildId, "now-playing");

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message != null ? $"> [ERROR]: {ex.Message}" : $"> [ERROR]: Something went wrong in {this.GetType().Name} : StopPlayingAsync()");
            }
        }

        public async Task SkipSongAsync(ulong guildId, SocketSlashCommand? command = null, SocketMessageComponent? component = null)
        {
            // This method assumes that only either command or component has a value.
            // Both SocketSlashCommand and SocketMessageComponent cannot have a value at the same time

            try
            {
                GuildData guildData = _guildDataDict.TryGetValue(guildId, out var foundGuild) ? foundGuild : throw new Exception($"GuildData was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");

                if (guildData.cTokenSource == null)
                {
                    throw new Exception($"CancellationTokenSource was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                }

                // Get the valid object from parameters
                var validObject = GetValidInteractionObject(command, component).Result;

                switch (validObject)
                {
                    case SocketSlashCommand:
                        if (validObject is SocketSlashCommand validCommand && validCommand.User is SocketGuildUser commandUser)
                        {
                            // Check if user and bot are in the same voice channel
                            if (commandUser.VoiceChannel == null || !commandUser.VoiceChannel.Users.Any(u => u.Id == _client.CurrentUser.Id))
                            {
                                await SendErrorResponseAsync("skip-wrong-channel", validCommand);

                                return;
                            }

                            await validCommand.DeferAsync();

                            await SendResponseAsync(guildId, "song-skipped", validCommand);
                        }
                        break;
                    case SocketMessageComponent:
                        if (validObject is SocketMessageComponent validComponent && validComponent.User is SocketGuildUser componentUser)
                        {
                            // Check if user and bot are in the same voice channel
                            if (componentUser.VoiceChannel == null || !componentUser.VoiceChannel.Users.Any(u => u.Id == _client.CurrentUser.Id))
                            {
                                await SendErrorResponseAsync("skip-wrong-channel", null, validComponent);

                                return;
                            }

                            await validComponent.DeferAsync();

                            await SendResponseAsync(guildId, "song-skipped", null, validComponent);
                        }

                        break;
                }

                await DisableButtons(guildId, "now-playing");

                // Request cancel last or you will end up with a race condition
                guildData.cTokenSource.Cancel();

                // Song list skipping etc is handled at StreamAudio() so no need to anything else here

                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message != null ? $"> [ERROR]: {ex.Message}" : $"> [ERROR]: Something went wrong in {this.GetType().Name} : SkipSongAsync()");

                await SendErrorResponseAsync("error", command);
            }

            return;
        }

        public async Task ClearQueueAsync(ulong guildId, SocketSlashCommand? command = null, SocketMessageComponent? component = null)
        {
            // This method assumes that only either command or component has a value.
            // Both SocketSlashCommand and SocketMessageComponent cannot have a value at the same time

            try
            {
                GuildData guildData = _guildDataDict.TryGetValue(guildId, out var foundGuild) ? foundGuild : throw new Exception($"GuildData was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");

                // Get the valid object from parameters
                var validObject = GetValidInteractionObject(command, component).Result;

                switch (validObject)
                {
                    case SocketSlashCommand:
                        if (validObject is SocketSlashCommand validCommand && validCommand.User is SocketGuildUser commandUser)
                        {
                            // Check if user and bot are in the same voice channel
                            if (commandUser.VoiceChannel == null || !commandUser.VoiceChannel.Users.Any(u => u.Id == _client.CurrentUser.Id))
                            {
                                await SendErrorResponseAsync("clear-queue-wrong-channel", validCommand);

                                return;
                            }

                            await validCommand.DeferAsync();

                            // Replace the song queue tied to guild, but include the currently playing song in the new queue
                            guildData.Queue = new List<SongData>() { guildData.Queue[0] };

                            _guildDataDict.TryUpdate(guildId, foundGuild, foundGuild);

                            await SendResponseAsync(guildId, "queue-cleared", validCommand);
                        }
                        break;
                    case SocketMessageComponent:
                        if (validObject is SocketMessageComponent validComponent && validComponent.User is SocketGuildUser componentUser)
                        {
                            // Check if user and bot are in the same voice channel
                            if (componentUser.VoiceChannel == null || !componentUser.VoiceChannel.Users.Any(u => u.Id == _client.CurrentUser.Id))
                            {
                                await SendErrorResponseAsync("clear-queue-wrong-channel", null, validComponent);

                                return;
                            }

                            await validComponent.DeferAsync();

                            // Replace the song queue tied to guild, but include the currently playing song in the new queue
                            guildData.Queue = new List<SongData>() { guildData.Queue[0] };

                            _guildDataDict.TryUpdate(guildId, foundGuild, foundGuild);

                            await SendResponseAsync(guildId, "queue-cleared", null, validComponent);
                        }
                        break;
                }

                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message != null ? $"> [ERROR]: {ex.Message}" : $"> [ERROR]: Something went wrong in {this.GetType().Name} : ClearQueueAsync()");

                await SendErrorResponseAsync("error", command);
            }
        }

        public async Task CheckChannelStateAsync(SocketVoiceChannel channel)
        {
            try
            {
                // SocketVoiceChannel.Users will return all users that connected, so we need to check if users voice channel is null to get actual number of users
                int userCount = channel.Users.Where(c => c.VoiceChannel != null).Count();

                // Disconnect if channel is empty
                if (userCount <= 1)
                {
                    await channel.DisconnectAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message != null ? $"> [ERROR]: {ex.Message}" : $"> [ERROR]: Something went wrong in {this.GetType().Name} : CheckChannelStateAsync()");
            }
        }

        public async Task DisposeGuildResourcesAsync(ulong guildId)
        {
            try
            {
                foreach (var entry in _guildDataDict)
                {
                    if (entry.Key == guildId)
                    {
                        // Disable buttons from last "now-playing" before deleting
                        await DisableButtons(entry.Key, "now-playing");

                        _guildDataDict.TryRemove(entry.Key, out _);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message != null ? $"> [ERROR]: {ex.Message}" : $"> [ERROR]: Something went wrong in {this.GetType().Name} : DisposeGuildResourcesAsync()");
            }
        }

        private CancellationTokenSource UpdateOrAddCancellationTokenSource(ulong guildId)
        {
            // Updae cTokenSource property of GuildData
            if (_guildDataDict.TryGetValue(guildId, out var foundGuild))
            {
                foundGuild.cTokenSource = new CancellationTokenSource();

                // Update guild data
                _guildDataDict.TryUpdate(guildId, foundGuild, foundGuild);

                return foundGuild.cTokenSource;
            }

            throw new Exception($"GuildData object was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
        }

        private Task<object> GetValidInteractionObject(SocketSlashCommand? command = null, SocketMessageComponent? component = null)
        {
            if (command != null)
            {
                return Task.FromResult<object>(command);
            }
            else if (component != null)
            {
                return Task.FromResult<object>(component);
            }

            throw new Exception($"SocketSlashCommand and SocketMessageComponent were null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
        }

        private async Task AppendQueryToQueueAsync(SocketSlashCommand command)
        {
            if (command.GuildId is ulong guildId && _guildDataDict.TryGetValue(guildId, out var foundGuild))
            {
                // Create song object with slashcommands first parameter and add it to queue
                SongData song = _ytDlp.GetSongFromSlashCommand(command);

                foundGuild.Queue.Add(song);

                _guildDataDict.TryUpdate(guildId, foundGuild, foundGuild);

                // Don't respond with "user-requested" if queue count is 1
                if (foundGuild.Queue.Count > 1)
                {
                    await SendResponseAsync(guildId, "user-requested", command);
                }
            }

            return;
        }

        private Task StreamAudio(SocketSlashCommand command)
        {
            if (command.GuildId is not ulong guildId)
            {
                throw new Exception($"Command GuildID was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }

            if (!_guildDataDict.TryGetValue(guildId, out var guildData) || guildData.Queue == null || guildData.AudioClient == null || guildData.cTokenSource == null)
            {
                throw new Exception($"Guild data was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }

            List<SongData> queue = guildData.Queue;
            IAudioClient audioClient = guildData.AudioClient;
            CancellationTokenSource cTokenSource = guildData.cTokenSource;

            // Create task with cancellation token tied to guild id
            Task streamAudioTask = new Task(async () =>
            {
                while (queue.Count > 0)
                {
                    using (var ffmpegStream = _ffmpeg.GetAudioStreamFromUrl(queue[0].AudioUrl))
                    using (var outStream = audioClient.CreatePCMStream(AudioApplication.Music, bufferMillis: 1000))
                    {
                        try
                        {
                            // Display what we're playing in a response
                            await SendResponseAsync(guildId, "now-playing", command);

                            // Write to outStream in pieces rather than all at once
                            byte[] buffer = new byte[8192];
                            int bytesRead;

                            while ((bytesRead = await ffmpegStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                cTokenSource.Token.ThrowIfCancellationRequested();

                                await outStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                            }

                        }
                        catch
                        {
                            // Rewnew CancellationTokenSource, update guild cts with it and replace old cTokenSource with the new one
                            cTokenSource = UpdateOrAddCancellationTokenSource(guildId);
                        }
                        finally
                        {
                            // Flush the audio stream for clean playback
                            await outStream.FlushAsync();

                            ffmpegStream.Dispose();

                            // Get updated version of queue
                            var guildData = _guildDataDict.TryGetValue(guildId, out var foundGuild) ? foundGuild : throw new Exception($"List<SongData> was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                            queue = guildData.Queue;

                            // Set first song boolean to false to influense responding behaviour
                            guildData.FirstSong = false;

                            // Remove the song we just played from the queue
                            queue.RemoveAt(0);

                            // Disable buttons of the just played songs now-playing notification
                            await DisableButtons(guildId, "now-playing");

                            // Check if songlist is empty. Set firstSong to true if its empty
                            if (queue.Count <= 0)
                            {
                                guildData.FirstSong = true;
                            }

                            // Update guild data
                            _guildDataDict.TryUpdate(guildId, foundGuild, foundGuild);

                        }

                    }
                }
            }, cTokenSource.Token);

            // Don't start another task if queue count is more than 1
            if (queue.Count <= 1)
            {
                streamAudioTask.Start();
            }

            return Task.CompletedTask;
        }

        private async Task SendResponseAsync(ulong guildId, string type, SocketSlashCommand? command = null, SocketMessageComponent? component = null)
        {
            // Get the valid object from parameters
            var validObject = GetValidInteractionObject(command, component).Result;

            GuildData guildData = _guildDataDict.TryGetValue(guildId, out var foundGuild) ? foundGuild : throw new Exception($"GuildData was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            List<SongData> queue = guildData.Queue;

            var embedBuilder = new EmbedBuilder();
            var componentBuilder = new ComponentBuilder();

            switch (validObject)
            {
                case SocketSlashCommand:
                    if (validObject is not SocketSlashCommand validCommand || validCommand.GuildId is not ulong commandGuildId || validCommand.User is not SocketGuildUser commandUser)
                    {
                        throw new Exception($"SocketSlashCommand was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                    }

                    switch (type)
                    {
                        case "song-skipped":
                            embedBuilder.Author = new EmbedAuthorBuilder
                            {
                                IconUrl = null,
                                Name = $"{validCommand.User.GlobalName} skipped a song in {commandUser.VoiceChannel}",
                                Url = null
                            };
                            embedBuilder.Description = $"[{queue[0].Title}]({queue[0].VideoUrl})";
                            embedBuilder.ThumbnailUrl = queue[0].ThumbnailUrl;
                            embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = commandUser.Guild.Name, IconUrl = commandUser.Guild.IconUrl });

                            await validCommand.ModifyOriginalResponseAsync(msg =>
                            {
                                msg.Embeds = new[] { embedBuilder.Build() };
                            });
                            break;
                        case "queue-cleared":
                            embedBuilder.Author = new EmbedAuthorBuilder
                            {
                                IconUrl = null,
                                Name = $"{validCommand.User.GlobalName} cleared the queue in {commandUser.VoiceChannel}",
                                Url = null
                            };
                            embedBuilder.Description = $"Use /play to add more songs to the queue";
                            embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = commandUser.Guild.Name, IconUrl = commandUser.Guild.IconUrl });

                            await validCommand.ModifyOriginalResponseAsync(msg =>
                            {
                                msg.Embeds = new[] { embedBuilder.Build() };
                            });
                            break;
                        case "user-requested":
                            embedBuilder.Author = new EmbedAuthorBuilder
                            {
                                IconUrl = null,
                                Name = $"{validCommand.User.GlobalName} added a new song to the queue",
                                Url = null
                            };
                            embedBuilder.Description = $"[{queue[queue.Count - 1].Title}]({queue[queue.Count - 1].VideoUrl})";
                            embedBuilder.ThumbnailUrl = queue[queue.Count - 1].ThumbnailUrl;
                            embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = commandUser.Guild.Name, IconUrl = commandUser.Guild.IconUrl });

                            if (queue.Count > 2)
                            {
                                if (embedBuilder.Fields == null || embedBuilder.Fields.Count <= 0)
                                {
                                    embedBuilder.Fields = new List<EmbedFieldBuilder>
                                    {
                                        new EmbedFieldBuilder
                                        {
                                            Name = "Songs in queue",
                                            Value = $"- {queue[1].Title} \n",
                                        }
                                    };
                                }

                                for (int i = 2; i != queue.Count; i++)
                                {
                                    embedBuilder.Fields[0].Value += $"- {queue[i].Title} \n";
                                }
                            }

                            await validCommand.ModifyOriginalResponseAsync(msg =>
                            {
                                msg.Embeds = new[] { embedBuilder.Build() };
                            });
                            break;
                        case "stopped-playing":
                            embedBuilder.Author = new EmbedAuthorBuilder
                            {
                                IconUrl = null,
                                Name = $"{validCommand.User.GlobalName} stopped playing in {commandUser.VoiceChannel}",
                                Url = null
                            };
                            embedBuilder.Description = $"Use /play to continue listening";
                            embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = commandUser.Guild.Name, IconUrl = commandUser.Guild.IconUrl });

                            await validCommand.ModifyOriginalResponseAsync(msg =>
                            {
                                msg.Embeds = new[] { embedBuilder.Build() };
                            });
                            break;
                        case "now-playing":
                            embedBuilder.Author = new EmbedAuthorBuilder
                            {
                                IconUrl = null,
                                Name = $"Now playing in {commandUser.VoiceChannel}",
                                Url = null
                            };
                            embedBuilder.Title = queue[0].Title;
                            embedBuilder.Url = queue[0].VideoUrl;
                            embedBuilder.Fields = new List<EmbedFieldBuilder>
                            {
                                new EmbedFieldBuilder
                                {
                                    Name = "Duration" ,
                                    Value = $"{queue[0].Duration.Hours:D2}:{queue[0].Duration.Minutes:D2}",
                                    IsInline = true
                                },
                                new EmbedFieldBuilder
                                {
                                    Name = "Queue" ,
                                    Value = $"{queue.Count - 1}",
                                    IsInline = true
                                }
                            };
                            embedBuilder.ImageUrl = queue[0].ThumbnailUrl;
                            embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = commandUser.Guild.Name, IconUrl = commandUser.Guild.IconUrl });

                            // Add "Next song" field if we have more than 1 song in queue
                            if (queue.Count > 1)
                            {
                                embedBuilder.Fields.Add(new EmbedFieldBuilder
                                {
                                    Name = "Next in queue",
                                    Value = $"{queue[1].Title ?? "Title"}",
                                    IsInline = false
                                });
                            };

                            var buttons = new List<IMessageComponent>
                            {
                                new ButtonBuilder()
                                    .WithLabel("Stop playing")
                                    .WithStyle(ButtonStyle.Secondary)
                                    .WithCustomId("embed-stop-playing-button")
                                    .WithDisabled(false).Build(),

                                new ButtonBuilder()
                                    .WithLabel("Clear queue")
                                    .WithStyle(ButtonStyle.Secondary)
                                    .WithCustomId("embed-clear-queue-button")
                                    .WithDisabled(false).Build(),

                                new ButtonBuilder()
                                    .WithLabel("Skip song")
                                    .WithStyle(ButtonStyle.Secondary)
                                    .WithCustomId("embed-skip-button")
                                    .WithDisabled(false).Build(),
                            };

                            var rowBuilder = new ActionRowBuilder()
                                    .WithComponents(buttons);

                            // Send "Now playing" as independend message if
                            // - Songlist has 2 or more songs
                            // - firstSong is false
                            if (queue.Count > 1 || guildData.FirstSong == false)
                            {
                                // Store IUserMessage to guild data
                                guildData.UserMessage = (IUserMessage)await validCommand.Channel.SendMessageAsync(embeds: [embedBuilder.Build()], components: componentBuilder.WithRows(new[] { rowBuilder }).Build());
                            }
                            else
                            {
                                // Store IUserMessage to guild data
                                guildData.UserMessage = (IUserMessage)await validCommand.ModifyOriginalResponseAsync(msg =>
                                {
                                    msg.Embeds = new[] { embedBuilder.Build() };
                                    msg.Components = componentBuilder.WithRows(new[] { rowBuilder }).Build();
                                });
                            }

                            // Update guild
                            _guildDataDict.TryUpdate(guildId, guildData, guildData);
                            break;
                    }
                    break;
                case SocketMessageComponent:
                    if (validObject is not SocketMessageComponent validComponent || validComponent.GuildId is not ulong componentGuildId || validComponent.User is not SocketGuildUser componentUser)
                    {
                        throw new Exception($"SocketMessageComponent was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                    }

                    switch (type)
                    {
                        case "song-skipped":
                            embedBuilder.Author = new EmbedAuthorBuilder
                            {
                                IconUrl = null,
                                Name = $"{validComponent.User.GlobalName} skipped a song in {componentUser.VoiceChannel}",
                                Url = null
                            };
                            embedBuilder.Description = $"[{queue[0].Title}]({queue[0].VideoUrl})";
                            embedBuilder.ThumbnailUrl = queue[0].ThumbnailUrl;
                            embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = componentUser.Guild.Name, IconUrl = componentUser.Guild.IconUrl });

                            await validComponent.FollowupAsync(embeds: new[] { embedBuilder.Build() });
                            break;
                        case "queue-cleared":
                            embedBuilder.Author = new EmbedAuthorBuilder
                            {
                                IconUrl = null,
                                Name = $"{validComponent.User.GlobalName} cleared the queue in {componentUser.VoiceChannel}",
                                Url = null
                            };
                            embedBuilder.Description = $"Use /play to add more songs to the queue";
                            embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = componentUser.Guild.Name, IconUrl = componentUser.Guild.IconUrl });

                            await validComponent.FollowupAsync(embeds: new[] { embedBuilder.Build() });
                            break;
                        case "stopped-playing":
                            embedBuilder.Author = new EmbedAuthorBuilder
                            {
                                IconUrl = null,
                                Name = $"{validComponent.User.GlobalName} stopped playing in {componentUser.VoiceChannel}",
                                Url = null
                            };
                            embedBuilder.Description = $"Use /play to continue listening";
                            embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = componentUser.Guild.Name, IconUrl = componentUser.Guild.IconUrl });

                            await validComponent.FollowupAsync(embeds: new[] { embedBuilder.Build() });
                            break;
                    }
                    break;
            }

            return;
        }

        private async Task SendErrorResponseAsync(string type, SocketSlashCommand? command = null, SocketMessageComponent? component = null)
        {
            // Get the valid object from parameters
            var validObject = GetValidInteractionObject(command, component).Result;

            var embedBuilder = new EmbedBuilder();
            var componentBuilder = new ComponentBuilder();

            switch (validObject)
            {
                case SocketSlashCommand:
                    if (validObject is not SocketSlashCommand validCommand || validCommand.User is not SocketGuildUser commandUser)
                    {
                        throw new Exception($"SocketSlashCommand was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                    }

                    switch (type)
                    {
                        case "error":
                            embedBuilder.Title = $"Something went wrong";
                            embedBuilder.Description = $"Something went wrong while executing **/{validCommand.CommandName}**. ";
                            break;
                        case "channel-not-found":
                            embedBuilder.Title = $"Couldn't find the voice channel";
                            embedBuilder.Description = $"You should be connected to a voice channel before requesting **/{validCommand.CommandName}**. ";
                            break;
                        case "channel-full":
                            embedBuilder.Title = $"Couldn't connect to the voice channel";
                            embedBuilder.Description = $"The voice channel is at maximum capacity. You could kick your least favorite friend to make room.";
                            break;
                        case "stop-playing-wrong-channel":
                            embedBuilder.Title = $"Couldn't stop playing";
                            embedBuilder.Description = $"You must be connected to the same voice channel as the bot to execute **/{validCommand.CommandName}**.";
                            break;
                        case "skip-wrong-channel":
                            embedBuilder.Title = $"Couldn't skip the song";
                            embedBuilder.Description = $"You must be connected to the same voice channel as the bot to execute **/{validCommand.CommandName}**.";
                            break;
                        case "clear-queue-wrong-channel":
                            embedBuilder.Title = $"Couldn't clear the queue";
                            embedBuilder.Description = $"You must be connected to the same voice channel as the bot to execute **/{validCommand.CommandName}**.";
                            break;
                    }

                    embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = commandUser.Guild.Name, IconUrl = commandUser.Guild.IconUrl });

                    try
                    {
                        await validCommand.RespondAsync(embeds: new[] { embedBuilder.Build() }, ephemeral: true);
                    }
                    catch
                    {
                        await validCommand.ModifyOriginalResponseAsync(msg =>
                        {
                            msg.Embeds = new[] { embedBuilder.Build() };
                        });
                    }
                    break;
                case SocketMessageComponent:
                    if (validObject is not SocketMessageComponent validComponent || validComponent.User is not SocketGuildUser componentUser)
                    {
                        throw new Exception($"SocketMessageComponent was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                    }

                    switch (type)
                    {
                        case "error":
                            embedBuilder.Title = $"Something went wrong";
                            embedBuilder.Description = $"Something went wrong. ";
                            break;
                        case "stop-playing-wrong-channel":
                            embedBuilder.Title = $"Couldn't stop playing";
                            embedBuilder.Description = $"You must be connected to the same voice channel as the bot to stop playing.";
                            break;
                        case "skip-wrong-channel":
                            embedBuilder.Title = $"Couldn't skip the song";
                            embedBuilder.Description = $"You must be connected to the same voice channel as the bot to skip a song.";
                            break;
                        case "clear-queue-wrong-channel":
                            embedBuilder.Title = $"Couldn't clear the queue";
                            embedBuilder.Description = $"You must be connected to the same voice channel as the bot to clear queue.";
                            break;
                    }

                    embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = componentUser.Guild.Name, IconUrl = componentUser.Guild.IconUrl });

                    await validComponent.RespondAsync(embeds: new[] { embedBuilder.Build() }, ephemeral: true);
                    break;
            }

            return;
        }

        private async Task DisableButtons(ulong guildId, string id)
        {
            if (!_guildDataDict.TryGetValue(guildId, out var guildData) || guildData.UserMessage is not IUserMessage message)
            {
                throw new Exception($"GuildData was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }

            var componentBuilder = new ComponentBuilder();

            switch (id)
            {
                case "now-playing":
                    var buttons = new List<IMessageComponent>
                    {
                        new ButtonBuilder()
                                    .WithLabel("Stop playing")
                                    .WithStyle(ButtonStyle.Secondary)
                                    .WithCustomId("embed-stop-playing-button")
                                    .WithDisabled(true).Build(),

                        new ButtonBuilder()
                                .WithLabel("Clear queue")
                                .WithStyle(ButtonStyle.Secondary)
                                .WithCustomId("embed-clear-queue-button")
                                .WithDisabled(true).Build(),

                        new ButtonBuilder()
                                .WithLabel("Skip song")
                                .WithStyle(ButtonStyle.Secondary)
                                .WithCustomId("embed-skip-button")
                                .WithDisabled(true).Build(),
                    };

                    var rowBuilder = new ActionRowBuilder()
                            .WithComponents(buttons);

                    // Tranform message.IEmbed collection into Embed[]
                    var embeds = message.Embeds.Select(embed => embed as Embed).Where(e => e != null).ToArray();

                    await message.ModifyAsync(msg =>
                    {
                        msg.Embeds = embeds;
                        msg.Components = componentBuilder.WithRows(new[] { rowBuilder }).Build();
                    });
                    break;
            }

            return;
        }


    }
}
