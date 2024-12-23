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
        // - Quicker processing for playlists
        // - Print queue command

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
                        await SendErrorResponseAsync("channel-full", command);

                        return;
                    }
                    else if (guildData.AudioClient != null && guildData.AudioClient.ConnectionState == ConnectionState.Connected)
                    {
                        // Tell discord we acknowledge the interaction
                        await command.DeferAsync();

                        // Add songs to queue and start streaming audio
                        await AppendQueryToQueueAsync(command);

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

                        // Add songs to queue and start streaming audio
                        await AppendQueryToQueueAsync(command);
                        await StreamAudio(command);
                    }

                }
                else
                {
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
            // This method assumes that only either command or component has a value
            // Both SocketSlashCommand and SocketMessageComponent cannot have a value at the same time
            try
            {
                // Check which parameter isn't null
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

                            await RespondToSlashCommand(guildId, "stopped-playing", validCommand);

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

                            await RespondToMessageComponent(guildId, "stopped-playing", validComponent);

                            return;
                        }
                        break;
                }

                // Modify "now-playing" message
                await ModifyUserMessage(guildId, "now-playing");

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message != null ? $"> [ERROR]: {ex.Message}" : $"> [ERROR]: Something went wrong in {this.GetType().Name} : StopPlayingAsync()");
            }
        }

        public async Task SkipSongAsync(ulong guildId, SocketSlashCommand? command = null, SocketMessageComponent? component = null)
        {
            // This method assumes that only either command or component has a value
            // Both SocketSlashCommand and SocketMessageComponent cannot have a value at the same time
            try
            {
                GuildData guildData = _guildDataDict.TryGetValue(guildId, out var foundGuild) ? foundGuild : throw new Exception($"GuildData was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");

                if (guildData.cTokenSource == null)
                {
                    throw new Exception($"CancellationTokenSource was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                }

                // Check which parameter isn't null
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

                            await RespondToSlashCommand(guildId, "song-skipped", validCommand);
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

                            await RespondToMessageComponent(guildId, "song-skipped", validComponent);
                        }

                        break;
                }

                await ModifyUserMessage(guildId, "now-playing");

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
            // This method assumes that only either command or component has a value
            // Both SocketSlashCommand and SocketMessageComponent cannot have a value at the same time
            try
            {
                GuildData guildData = _guildDataDict.TryGetValue(guildId, out var foundGuild) ? foundGuild : throw new Exception($"GuildData was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");

                // Check which parameter isn't null
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

                            await RespondToSlashCommand(guildId, "queue-cleared", validCommand);
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

                            await RespondToMessageComponent(guildId, "queue-cleared", validComponent);
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
                // SocketVoiceChannel.Users will return every user that connected to voice channel. Check if Users.VoiceChannel is null to determine if they're connected
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
                        // Modify last "now-playing" before deleting
                        await ModifyUserMessage(entry.Key, "now-playing");

                        _guildDataDict.TryRemove(entry.Key, out _);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message != null ? $"> [ERROR]: {ex.Message}" : $"> [ERROR]: Something went wrong in {this.GetType().Name} : DisposeGuildResourcesAsync()");
            }
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
            if (command.GuildId is ulong guildId && _guildDataDict[guildId] != null)
            {
                // Get required information about the songs with yt-dlp
                List<SongData> songList = _ytDlp.GetSongFromSlashCommand(command);

                _guildDataDict[guildId].Queue.AddRange(songList);

                // songList having more than 1 song indicates that user has added a playlist. Provide feedback
                if (songList.Count > 1)
                {
                    await RespondToSlashCommand(guildId, "user-requested-playlist", command);
                }
                else if (_guildDataDict[guildId].Queue.Count > 1 && _guildDataDict[guildId].CurrentlyPlaying) // Skip providing feedback if queue count is only 1
                {
                    await RespondToSlashCommand(guildId, "user-requested", command);
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

            GuildData guild = _guildDataDict.TryGetValue(guildId, out var foundGuild) ? foundGuild : throw new Exception($"GuildData was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");

            // Create task with cancellation token tied to guild id
            Task streamAudioTask = new Task(async () =>
            {
                // Set guilds CurrentlyPlaying to true
                _guildDataDict[guildId].CurrentlyPlaying = true;

                List<SongData> queue = _guildDataDict[guildId].Queue ?? throw new Exception($"Guild List<SongData> was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}"); ;
                IAudioClient audioClient = _guildDataDict[guildId].AudioClient ?? throw new Exception($"Guild IAudioClient was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");

                while (queue.Count > 0)
                {
                    using (var ffmpegStream = _ffmpeg.GetAudioStreamFromUrl(queue[0].AudioUrl))
                    using (var outStream = audioClient.CreatePCMStream(AudioApplication.Music, bufferMillis: 1000))
                    {
                        try
                        {
                            // Display what we're playing in a response
                            await RespondToSlashCommand(guildId, "now-playing", command);

                            // Set FirstSong to false to influence response behaviour
                            _guildDataDict[guildId].FirstSong = false;

                            // Write to outStream in pieces rather than all at once
                            byte[] buffer = new byte[8192];
                            int bytesRead;

                            while ((bytesRead = await ffmpegStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                _guildDataDict[guildId].cTokenSource.Token.ThrowIfCancellationRequested();

                                await outStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                            }
                        }
                        catch
                        {
                            // Rewnew CancellationTokenSource for guild
                            _guildDataDict[guildId].cTokenSource = new CancellationTokenSource();
                        }

                        // Flush & dispose streams
                        await outStream.FlushAsync();
                        ffmpegStream.Dispose();

                        // Get updated version of queue
                        queue = _guildDataDict.TryGetValue(guildId, out var foundGuild) ? foundGuild.Queue : throw new Exception($"Guild List<SongData> was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");

                        // Remove the song we just played from queue
                        queue.RemoveAt(0);

                        await ModifyUserMessage(guildId, "now-playing");

                        // On empty queue set FirstSong to true and CurrentlyPlaying to false
                        if (queue.Count <= 0)
                        {
                            _guildDataDict[guildId].CurrentlyPlaying = false;
                            _guildDataDict[guildId].FirstSong = true;
                        }
                    }
                }
            }, guild.cTokenSource.Token);

            // Don't start another task if we're already playing
            if (!guild.CurrentlyPlaying)
            {
                streamAudioTask.Start();
            }

            return Task.CompletedTask;
        }

        private async Task RespondToSlashCommand(ulong guildId, string type, SocketSlashCommand command)
        {
            GuildData guildData = _guildDataDict.TryGetValue(guildId, out var foundGuild) ? foundGuild : throw new Exception($"GuildData was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");

            SocketGuildUser user = (SocketGuildUser)command.User;
            List<SongData> queue = guildData.Queue;

            var embedBuilder = new EmbedBuilder();
            var componentBuilder = new ComponentBuilder();
            var rowBuilder = new ActionRowBuilder();

            switch (type)
            {
                case "song-skipped":
                    embedBuilder.Author = new EmbedAuthorBuilder
                    {
                        IconUrl = null,
                        Name = $"{command.User.GlobalName} skipped a song in {user.VoiceChannel}",
                        Url = null
                    };
                    embedBuilder.Description = $"[{queue[0].Title}]({queue[0].VideoUrl})";
                    embedBuilder.ThumbnailUrl = queue[0].ThumbnailUrl;
                    embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = user.Guild.Name, IconUrl = user.Guild.IconUrl });
                    break;
                case "queue-cleared":
                    embedBuilder.Author = new EmbedAuthorBuilder
                    {
                        IconUrl = null,
                        Name = $"{command.User.GlobalName} cleared the queue in {user.VoiceChannel}",
                        Url = null
                    };
                    embedBuilder.Description = $"Use /play to add more songs to the queue";
                    embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = user.Guild.Name, IconUrl = user.Guild.IconUrl });
                    break;
                case "user-requested":
                    embedBuilder.Author = new EmbedAuthorBuilder
                    {
                        IconUrl = null,
                        Name = $"{command.User.GlobalName} added a new song to the queue",
                        Url = null
                    };
                    embedBuilder.Description = $"[{queue[queue.Count - 1].Title}]({queue[queue.Count - 1].VideoUrl})";
                    embedBuilder.ThumbnailUrl = queue[queue.Count - 1].ThumbnailUrl;
                    embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = user.Guild.Name, IconUrl = user.Guild.IconUrl });

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
                            if (i <= 8)
                            {
                                embedBuilder.Fields[0].Value += $"- {queue[i].Title} \n";
                            }
                            else if (i == 9 && queue.Count == 10)
                            {
                                embedBuilder.Fields[0].Value += $"- And 1 other song...";
                            }
                            else
                            {
                                embedBuilder.Fields[0].Value += $"- And {queue.Count - 9} other songs...";

                                break;
                            }
                        }
                    }
                    break;
                case "user-requested-playlist":
                    embedBuilder.Author = new EmbedAuthorBuilder
                    {
                        IconUrl = null,
                        Name = $"{command.User.GlobalName} added a new playlist to the queue",
                        Url = null
                    };
                    embedBuilder.Description = $"[{queue[queue.Count - 1].Title}]({queue[queue.Count - 1].VideoUrl})";
                    embedBuilder.ThumbnailUrl = queue[queue.Count - 1].ThumbnailUrl;
                    embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = user.Guild.Name, IconUrl = user.Guild.IconUrl });

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
                            if (i <= 8)
                            {
                                embedBuilder.Fields[0].Value += $"- {queue[i].Title} \n";
                            }
                            else if (i == 9 && queue.Count == 10)
                            {
                                embedBuilder.Fields[0].Value += $"- And 1 other song...";
                            }
                            else
                            {
                                embedBuilder.Fields[0].Value += $"- And {queue.Count - 9} other songs...";

                                break;
                            }
                        }
                    }
                    break;
                case "stopped-playing":
                    embedBuilder.Author = new EmbedAuthorBuilder
                    {
                        IconUrl = null,
                        Name = $"{command.User.GlobalName} stopped playing in {user.VoiceChannel}",
                        Url = null
                    };
                    embedBuilder.Description = $"Use /play to continue listening";
                    embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = user.Guild.Name, IconUrl = user.Guild.IconUrl });
                    break;
                case "now-playing":
                    embedBuilder.Author = new EmbedAuthorBuilder
                    {
                        IconUrl = null,
                        Name = $"Now playing in {user.VoiceChannel}",
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
                    embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = user.Guild.Name, IconUrl = user.Guild.IconUrl });

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

                    // Add buttons
                    rowBuilder.WithComponents(new List<IMessageComponent>
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
                    });
                    break;
            }

            // Use custom logic for "now-playing"
            if (type == "now-playing")
            {
                if ((queue.Count > 1 && guildData.FirstSong == false) || (queue.Count > 1 && guildData.CurrentlyPlaying))
                {
                    // Store IUserMessage to guild data
                    _guildDataDict[guildId].NowPlayingMessage = (IUserMessage)await command.Channel.SendMessageAsync(embeds: [embedBuilder.Build()], components: componentBuilder.WithRows(new[] { rowBuilder }).Build());
                }
                else
                {
                    // Store IUserMessage to guild data
                    _guildDataDict[guildId].NowPlayingMessage = (IUserMessage)await command.ModifyOriginalResponseAsync(msg =>
                    {
                        msg.Embeds = new[] { embedBuilder.Build() };
                        msg.Components = componentBuilder.WithRows(new[] { rowBuilder }).Build();
                    });
                }

                return;
            }

            await command.ModifyOriginalResponseAsync(msg =>
            {
                msg.Embeds = new[] { embedBuilder.Build() };
                msg.Components = componentBuilder.WithRows(new[] { rowBuilder }).Build();
            });

            return;
        }

        private async Task RespondToMessageComponent(ulong guildId, string type, SocketMessageComponent component)
        {
            GuildData guildData = _guildDataDict.TryGetValue(guildId, out var foundGuild) ? foundGuild : throw new Exception($"GuildData was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");

            SocketGuildUser user = (SocketGuildUser)component.User;
            List<SongData> queue = guildData.Queue;

            var embedBuilder = new EmbedBuilder();
            var componentBuilder = new ComponentBuilder();

            switch (type)
            {
                case "song-skipped":
                    embedBuilder.Author = new EmbedAuthorBuilder
                    {
                        IconUrl = null,
                        Name = $"{component.User.GlobalName} skipped a song in {user.VoiceChannel}",
                        Url = null
                    };
                    embedBuilder.Description = $"[{queue[0].Title}]({queue[0].VideoUrl})";
                    embedBuilder.ThumbnailUrl = queue[0].ThumbnailUrl;
                    embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = user.Guild.Name, IconUrl = user.Guild.IconUrl });
                    break;
                case "queue-cleared":
                    embedBuilder.Author = new EmbedAuthorBuilder
                    {
                        IconUrl = null,
                        Name = $"{component.User.GlobalName} cleared the queue in {user.VoiceChannel}",
                        Url = null
                    };
                    embedBuilder.Description = $"Use /play to add more songs to the queue";
                    embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = user.Guild.Name, IconUrl = user.Guild.IconUrl });
                    break;
                case "stopped-playing":
                    embedBuilder.Author = new EmbedAuthorBuilder
                    {
                        IconUrl = null,
                        Name = $"{component.User.GlobalName} stopped playing in {user.VoiceChannel}",
                        Url = null
                    };
                    embedBuilder.Description = $"Use /play to continue listening";
                    embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = user.Guild.Name, IconUrl = user.Guild.IconUrl });
                    break;
            }

            await component.FollowupAsync(embeds: new[] { embedBuilder.Build() });

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

        private async Task ModifyUserMessage(ulong guildId, string id)
        {
            if (!_guildDataDict.TryGetValue(guildId, out var guildData) || guildData.NowPlayingMessage is not IUserMessage message)
            {
                throw new Exception($"GuildData was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }

            Embed?[]? embeds = Array.Empty<Embed>();

            var componentBuilder = new ComponentBuilder();
            var rowBuilder = new ActionRowBuilder();

            switch (id)
            {
                case "now-playing":
                    // Tranform message.IEmbed collection into Embed[]
                    embeds = message.Embeds.Select(embed => embed as Embed).Where(e => e != null).ToArray();
                    break;
            }

            await message.ModifyAsync(msg =>
            {
                msg.Embeds = embeds;
                msg.Components = componentBuilder.WithRows(new[] { rowBuilder }).Build();
            });

            return;
        }


    }
}
