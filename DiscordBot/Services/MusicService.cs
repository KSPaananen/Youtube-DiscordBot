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
        private IConfigurationRepository _configurationRepository;

        private IYtDlp _ytDlp;
        private IFFmpeg _ffmpeg;

        private ConcurrentDictionary<ulong, GuildData> _guildData;

        private string _discordLink;

        public MusicService(IConfigurationRepository configurationRepository, IYtDlp ytDlp, IFFmpeg ffmpeg)
        {
            _configurationRepository = configurationRepository ?? throw new NullReferenceException(nameof(configurationRepository));

            _ytDlp = ytDlp ?? throw new NullReferenceException(nameof(ytDlp));
            _ffmpeg = ffmpeg ?? throw new NullReferenceException(nameof(ffmpeg));

            _guildData = new();

            _discordLink = _configurationRepository.GetDiscordLink();
        }

        // ToDo
        // - Stop playing button?
        // - Disable Buttons from the last "now-playing" embed when song is over

        public async Task Play(SocketSlashCommand command)
        {
            try
            {
                if (command.User is not IGuildUser user || command.GuildId is not ulong guildId)
                {
                    throw new Exception($"> [ERROR]: SocketSlashCommand.User was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                }

                // Try finding an already existing guild in the dictionary
                var guildData = _guildData.TryGetValue(guildId, out var foundGuild) ? foundGuild : new GuildData();

                var voiceChannel = user.VoiceChannel;

                if (voiceChannel != null)
                {
                    // Execution order and logic:
                    // 1. Make sure channel has room for the bot
                    // 2. Check if we're already connected to a channel. Skip to next step if we have a valid IAudioClient
                    // 3. Connect to channel and create an IAudioClient. Throw an error if there was an issue

                    if (voiceChannel.UserLimit <= voiceChannel.GetUsersAsync().CountAsync().Result)
                    {
                        // Use Ephemeral response so other users don't see the issue
                        await RespondToSlashCommand("channel-full", command);

                        return;
                    }
                    else if (guildData.AudioClient != null && guildData.AudioClient.ConnectionState == ConnectionState.Connected)
                    {
                        // Tell discord we have received interaction
                        await DeferInteractionAsync(command);

                        // Add songs to queue
                        await AppendQueryToQueueAsync(command);

                        // Stream audio
                        await StreamAudio(command);

                        return;
                    }
                    else if (guildData.AudioClient == null || guildData.AudioClient.ConnectionState == ConnectionState.Disconnected)
                    {
                        guildData.AudioClient = await user.VoiceChannel.ConnectAsync(true, false, false, false);

                        // Attach methods to events
                        guildData.AudioClient.StreamDestroyed += StreamDestroyed;

                        // Check if we we're able to connect. Throw error if not
                        if (guildData.AudioClient == null || guildData.AudioClient.ConnectionState == ConnectionState.Disconnected)
                        {
                            throw new Exception($"Unable to create IAudioClient with IVoiceChannel.ConnectAsync in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                        }

                        // Set Guilds first song to true
                        guildData.FirstSong = true;

                        // Update _guildData with guildData object
                        _guildData.AddOrUpdate(guildId, guildData, (key, oldValue) => oldValue);

                        // Tell discord we have received interaction
                        await DeferInteractionAsync(command);

                        // Add songs to queue
                        await AppendQueryToQueueAsync(command);

                        // Stream audio
                        await StreamAudio(command);
                    }

                }
                else
                {
                    // Inform user with an ephemeral response that they should be in a voice channel before requesting /play
                    await RespondToSlashCommand("channel-not-found", command);

                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message != null ? $"> [ERROR]: {ex.Message}" : $"> [ERROR]: Something went wrong in {this.GetType().Name} : Play()");

                // Modify acknowledged command response with error details
                await RespondToSlashCommand("error", command);
            }

        }

        public async Task SkipSong(ulong guildId, SocketSlashCommand? command = null, SocketMessageComponent? component = null)
        {
            // This method assumes that only either command or component has a value.
            // Both SocketSlashCommand and SocketMessageComponent cannot have a value at the same time

            try
            {
                // Get guilds token source and request cancel with the token source
                GuildData guildData = _guildData.TryGetValue(guildId, out var foundGuild) ? foundGuild : throw new Exception($"GuildData was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                CancellationTokenSource cTokenSource = guildData.cTokenSource;

                if (cTokenSource == null)
                {
                    throw new Exception($"CancellationTokenSource was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                }

                // Tell discord that we acknowledge the interaction
                var validObject = await DeferInteractionAsync(command, component);

                switch (validObject)
                {
                    case SocketSlashCommand:

                        // This method has been set in a way that its ready to skip songs with
                        // Slashcommands and SocketMessageComponents, but use SocketMessageComponents for now

                        break;
                    case SocketMessageComponent:
                        await DisableComponentsButtons((SocketMessageComponent)validObject);

                        await RespondToMessageComponents("song-skipped", (SocketMessageComponent)validObject);

                        break;
                }

                // Cancel last or you will end up wth a race condition
                cTokenSource.Cancel();

                // Song list skipping etc is handled at StreamAudio() so no need to anything else here

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message == null ? $"> [ERROR]: {ex.Message}" : $"> [ERROR]: Something went wrong in {this.GetType().Name} : SkipSong()");
            }

            return;
        }

        public async Task ClearQueue(ulong guildId, SocketSlashCommand? command = null, SocketMessageComponent? component = null)
        {
            // This method assumes that only either command or component has a value.
            // Both SocketSlashCommand and SocketMessageComponent cannot have a value at the same time

            try
            {
                // Get guilds queue
                GuildData guildData = _guildData.TryGetValue(guildId, out var foundGuild) ? foundGuild : throw new Exception($"GuildData was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");

                // Tell discord we acknowledge the interaction
                var validObject = await DeferInteractionAsync(command, component);

                switch (validObject)
                {
                    case SocketSlashCommand:

                        // This method has been set in a way that its ready to skip songs with
                        // Slashcommands and SocketMessageComponents, but use SocketMessageComponents for now

                        break;
                    case SocketMessageComponent:
                        // Replace the song queue tied to guild, but include the currently playing song in the new queue
                        guildData.Queue = new List<SongData>() { guildData.Queue[0] };

                        // Update guild data
                        _guildData.TryUpdate(guildId, foundGuild, foundGuild);

                        await RespondToMessageComponents("queue-cleared", (SocketMessageComponent)validObject);

                        break;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message == null ? $"> [ERROR]: {ex.Message}" : $"> [ERROR]: Something went wrong in {this.GetType().Name} : ClearQueue()");
            }

            return;
        }

        private async Task<object> DeferInteractionAsync(SocketSlashCommand? command = null, SocketMessageComponent? component = null)
        {
            // This method assumes atleast one parameter has a value. Both can have values, but command is preferred

            if (command != null)
            {
                await command.DeferAsync();

                return command;
            }
            else if (component != null)
            {
                await component.DeferAsync();

                return component;
            }
            else
            {
                throw new Exception($"SocketSlashCommand and SocketMessageComponent were null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }

        }

        private CancellationTokenSource UpdateOrAddCancellationTokenSource(ulong guildId)
        {
            // Updae cTokenSource property of GuildData
            if (_guildData.TryGetValue(guildId, out var foundGuild))
            {
                foundGuild.cTokenSource = new CancellationTokenSource();

                // Update guild data
                _guildData.TryUpdate(guildId, foundGuild, foundGuild);

                return foundGuild.cTokenSource;
            }

            throw new Exception($"GuildData object was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
        }

        private async Task AppendQueryToQueueAsync(SocketSlashCommand command)
        {
            if (command.GuildId is ulong guildId && _guildData.TryGetValue(guildId, out var foundGuild))
            {
                // Create song object with slashcommands first parameter and add it to queue
                SongData song = _ytDlp.GetSongFromSlashCommand(command);

                foundGuild.Queue.Add(song);

                // Update guild data
                _guildData.TryUpdate(guildId, foundGuild, foundGuild);

                // Don't respond with "user-requested" if queue count is 1
                if (foundGuild.Queue.Count > 1)
                {
                    await RespondToSlashCommand("user-requested", command);
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

            if (!_guildData.TryGetValue(guildId, out var guildData) || guildData.Queue == null || guildData.AudioClient == null || guildData.cTokenSource == null)
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
                            await RespondToSlashCommand("now-playing", command);

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
                            // Flush the audio stream 
                            await outStream.FlushAsync();

                            // Dispose stream to save resources
                            ffmpegStream.Dispose();

                            // Get updated version of queue
                            var guildData = _guildData.TryGetValue(guildId, out var foundGuild) ? foundGuild : throw new Exception($"List<SongData> was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                            queue = guildData.Queue;
                            
                            // Remove the song we just played from the queue
                            queue.RemoveAt(0);

                            // Set FirstSong to false
                            guildData.FirstSong = false;

                            // Check if songlist is empty. Set firstSong to true if thats the case
                            if (queue.Count <= 0)
                            {
                                guildData.FirstSong = true;

                                // To Do:
                                // - Disable buttons from last "now-playing" message

                            }

                            // Update guild data
                            _guildData.TryUpdate(guildId, foundGuild, foundGuild);

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

        private async Task RespondToSlashCommand(string type, SocketSlashCommand command)
        {
            if (command == null || command.GuildId is not ulong guildId || command.User is not IGuildUser user)
            {
                throw new Exception($"SocketSlashCommand was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }

            var embedBuilder = new EmbedBuilder();
            var componentBuilder = new ComponentBuilder();

            // Handle errors in a separate switch tree
            if (type is ("error" or "error-skipping" or "channel-not-found" or "channel-full"))
            {
                switch (type)
                {
                    case "error":
                        embedBuilder.Title = $"Something went wrong :(";
                        embedBuilder.Description = $"Something went wrong while executing **/{command.CommandName}**. ";
                        break;
                    case "channel-not-found":
                        embedBuilder.Title = $"Couldn't find the voice channel";
                        embedBuilder.Description = $"You should be connected to a voice channel before requesting **/{command.CommandName}**. ";
                        break;
                    case "channel-full":
                        embedBuilder.Title = $"Couldn't connect to the voice channel";
                        embedBuilder.Description = $"The voice channel is at maximum capacity. You could kick your least favorite friend to make room.";
                        break;
                }

                // Insert default values to embed
                embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = command.User.GlobalName, IconUrl = command.User.GetAvatarUrl() });

                // Extend response with a discordlink if its configured in appsettings.json
                if (_discordLink != "")
                {
                    // Adjust extended description based on type
                    switch (type)
                    {
                        case "error":
                            embedBuilder.Description = embedBuilder.Description + $"\n\nPlease fill out a bug report at the developers discord server.";
                            break;
                        default:
                            embedBuilder.Description = embedBuilder.Description + $"\n\nIf you believe this is a bug, please fill out a bug report at the developers discord server.";
                            break;
                    }

                    embedBuilder.Fields.Add(new EmbedFieldBuilder
                    {
                        Name = $"Discord server",
                        Value = _discordLink,
                        IsInline = true
                    });
                }

                // Try first responding to the command. On error modify response
                try
                {
                    await command.RespondAsync(embeds: [embedBuilder.Build()], ephemeral: true);
                }
                catch
                {
                    await command.ModifyOriginalResponseAsync(msg =>
                    {
                        msg.Embeds = new[] { embedBuilder.Build() };
                    });
                }

                return;
            }

            var guildData = _guildData.TryGetValue(guildId, out var foundGuild) ? foundGuild : throw new Exception($"GuildData was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            List<SongData> queue = guildData.Queue;

            // Succesful responses are handled in this switch tree
            switch (type)
            {
                case "user-requested":
                    embedBuilder.Author = new EmbedAuthorBuilder
                    {
                        IconUrl = "",
                        Name = $"Added a new song to the queue",
                        Url = ""
                    };
                    embedBuilder.Title = queue[queue.Count - 1].Title;
                    embedBuilder.Url = queue[queue.Count - 1].VideoUrl;
                    embedBuilder.ThumbnailUrl = queue[queue.Count - 1].ThumbnailUrl;
                    embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = user.GlobalName, IconUrl = user.GetAvatarUrl() });

                    if (queue.Count > 2)
                    {
                        if (embedBuilder.Fields == null || embedBuilder.Fields.Count <= 0)
                        {
                            embedBuilder.Fields = new List<EmbedFieldBuilder>
                            {
                                new EmbedFieldBuilder
                                {
                                    Name = "Songs in the queue",
                                    Value = $"- {queue[1].Title} \n",
                                }
                            };
                        }

                        for (int i = 2; i != queue.Count; i++)
                        {
                            embedBuilder.Fields[0].Value += $"- {queue[i].Title} \n";
                        }
                    }

                    await command.ModifyOriginalResponseAsync(msg =>
                    {
                        msg.Embeds = new[] { embedBuilder.Build() };
                    });
                    break;
                case "now-playing":
                    // Configure embedBuilder
                    embedBuilder.Author = new EmbedAuthorBuilder
                    {
                        IconUrl = null,
                        Name = user.VoiceChannel == null ? "Now playing" : $"Now playing in {user.VoiceChannel}",
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
                            Name = "Songs in queue" ,
                            Value = $"{queue.Count - 1}",
                            IsInline = true
                        }
                    };
                    embedBuilder.ImageUrl = queue[0].ThumbnailUrl;
                    embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = user.GlobalName, IconUrl = user.GetAvatarUrl() });

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

                    // Configure components
                    var buttons = new List<IMessageComponent>
                    {
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
                        await command.Channel.SendMessageAsync(embeds: [embedBuilder.Build()], components: componentBuilder.WithRows(new[] { rowBuilder }).Build());
                    }
                    else
                    {
                        await command.ModifyOriginalResponseAsync(msg =>
                        {
                            msg.Embeds = new[] { embedBuilder.Build() };
                            msg.Components = componentBuilder.WithRows(new[] { rowBuilder }).Build();
                        });
                    }
                    break;
                default:

                    break;

            }

            return;
        }

        private async Task RespondToMessageComponents(string type, SocketMessageComponent component)
        {
            if (component == null || component.GuildId is not ulong guildId || component.User is not IGuildUser user)
            {
                throw new Exception($"SocketMessageComponent was null in{this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }

            var embedBuilder = new EmbedBuilder();
            var componentBuilder = new ComponentBuilder();

            var guildData = _guildData.TryGetValue(guildId, out var foundGuild) ? foundGuild : throw new Exception($"GuildData was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            List<SongData> queue = guildData.Queue;

            switch (type)
            {
                case "song-skipped":
                    embedBuilder.Author = new EmbedAuthorBuilder
                    {
                        IconUrl = null,
                        Name = $"Song skipped in {user.VoiceChannel}",
                        Url = null
                    };
                    embedBuilder.Title = queue[0].Title;
                    embedBuilder.Url = queue[0].VideoUrl;
                    embedBuilder.ThumbnailUrl = queue[0].ThumbnailUrl;
                    embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = user.GlobalName, IconUrl = user.GetAvatarUrl() });

                    await component.FollowupAsync(embeds: new[] { embedBuilder.Build() });
                    break;
                case "queue-cleared":
                    embedBuilder.Author = new EmbedAuthorBuilder
                    {
                        IconUrl = null,
                        Name = $"Queue cleared in {user.VoiceChannel}",
                        Url = null
                    };
                    embedBuilder.Description = $"Use /play to add more songs to the queue";
                    embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = user.GlobalName, IconUrl = user.GetAvatarUrl() });

                    await component.FollowupAsync(embeds: new[] { embedBuilder.Build() });
                    break;
                default:

                    break;
            }

            return;
        }

        private async Task DisableComponentsButtons(SocketMessageComponent component)
        {
            if (component == null)
            {
                throw new Exception($"SocketMessageComponent was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }

            var componentBuilder = new ComponentBuilder();

            switch (component.Data.CustomId)
            {
                case "embed-skip-button" or "embed-stop-button":
                    // Get components previous message and create new buttons to replace its old ones
                    var originalEmbeds = component.Message.Embeds.First();

                    var buttons = new List<IMessageComponent>
                    {
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

                    await component.ModifyOriginalResponseAsync(msg =>
                    {
                        msg.Embeds = new[] { originalEmbeds };
                        msg.Components = componentBuilder.WithRows(new[] { rowBuilder }).Build();
                    });
                    break;
                default:
                    break;
            }

            return;
        }

        private Task StreamDestroyed(ulong streamId)
        {
            // ToDo:
            // - Figure out a more resource efficient solution here

            // Iterate through guild data dictionary looking for stream id
            foreach (var entry in _guildData)
            {
                // If stream id's match, delete guild from dictionary
                if (entry.Value.StreamID == streamId)
                {
                    _guildData.TryRemove(entry.Key, out _);
                }
            }

            return Task.CompletedTask;
        }


    }
}
