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

        private ConcurrentDictionary<ulong, List<Song>> _guildQueues; // Stores song queue
        private ConcurrentDictionary<ulong, bool> _guildFirstSong; // Stores boolean which marks if first song has been played
        private ConcurrentDictionary<ulong, CancellationTokenSource> _guildcTokenSources; // Stores task CancellationTokenSources
        private ConcurrentDictionary<ulong, IAudioClient> _guildAudioClients; // Stores audioclients
        private ConcurrentDictionary<ulong, ulong> _guildStreams; // Stored Stream ID's. Order => StreamID, GuildID

        private string _discordLink;

        public MusicService(IConfigurationRepository configurationRepository, IYtDlp ytDlp, IFFmpeg ffmpeg)
        {
            _configurationRepository = configurationRepository ?? throw new NullReferenceException(nameof(configurationRepository));

            _ytDlp = ytDlp ?? throw new NullReferenceException(nameof(ytDlp));
            _ffmpeg = ffmpeg ?? throw new NullReferenceException(nameof(ffmpeg));

            _guildQueues = new();
            _guildFirstSong = new();
            _guildcTokenSources = new();
            _guildAudioClients = new();
            _guildStreams = new();

            _discordLink = _configurationRepository.GetDiscordLink();
        }

        // ToDo
        // - Buttons for embeds
        // - Better error logic

        public async Task Play(SocketSlashCommand command)
        {
            try
            {
                if (command.User is not IGuildUser user || command.GuildId is not ulong guildId)
                {
                    throw new Exception($"> [ERROR]: SocketSlashCommand.User was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                }
                var voiceChannel = user.VoiceChannel;
                var audioClient = _guildAudioClients.TryGetValue(guildId, out var foundClient) ? foundClient : null;

                if (voiceChannel != null)
                {
                    // Execution order and logic:
                    // 1. Make sure channel has room for the bot
                    // 2. Check if we're already connected to a channel. Skip to next step if we have a valid IAudioClient
                    // 3. Connect to channel and create an IAudioClient. Throw an error if there was an issue

                    if (voiceChannel.UserLimit <= voiceChannel.GetUsersAsync().CountAsync().Result)
                    {
                        // Use Ephemeral response so other users don't see the issue
                        await ConstructResponses("channel-full", command);

                        return;
                    }
                    else if (audioClient != null && audioClient.ConnectionState == ConnectionState.Connected)
                    {
                        // Send an acknowledgement of the command
                        await AcknowledgeCommand(command);

                        // Add songs to queue
                        await AppendQueryToQueueAsync(command);

                        // Stream audio
                        await StreamAudio(command, audioClient);

                        return;
                    }
                    else if (audioClient == null || audioClient.ConnectionState == ConnectionState.Disconnected)
                    {
                        IAudioClient connectedAudioClient = await user.VoiceChannel.ConnectAsync(true, false, false, false);

                        // Get stream id and tie it to guild id
                        var streamId = connectedAudioClient.GetStreams().First().Key;
                        _guildStreams.TryAdd(streamId, (ulong)command.GuildId);

                        // Attach methods to events
                        connectedAudioClient.StreamDestroyed += StreamDestroyed;

                        if (connectedAudioClient == null || connectedAudioClient.ConnectionState == ConnectionState.Disconnected)
                        {
                            throw new Exception($"> [ERROR]: Unable to create IAudioClient with IVoiceChannel.ConnectAsync in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                        }

                        // Tie audioclient variable to a guild id
                        _guildAudioClients.GetOrAdd(guildId, connectedAudioClient);

                        // On connect set first song to true
                        _guildFirstSong.GetOrAdd(guildId, true);

                        // Send an acknowledgement of the command
                        await AcknowledgeCommand(command);

                        // Add songs to queue
                        await AppendQueryToQueueAsync(command);

                        // Stream audio
                        await StreamAudio(command, connectedAudioClient);
                    }

                }
                else
                {
                    // Inform user with an ephemeral response that they should be in a voice channel before requesting /play
                    await ConstructResponses("channel-not-found", command);

                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message  == null ? $"[ERROR]: {ex.Message}" : $"[ERROR]: Something went wrong in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");

                // Modify acknowledged command response with error details
                await ConstructResponses("error", command);
            }

        }

        public async Task SkipSong(ulong GuildId, SocketMessageComponent component)
        {
            try
            {
                // Get guilds token source and request cancel
                var cTokenSource = _guildcTokenSources.TryGetValue(GuildId, out var foundcTokenSource) ? foundcTokenSource : null;

                if (cTokenSource != null)
                {
                    cTokenSource.Cancel();
                }

                await ConstructResponses("song-skipped", component: component);

                // Song list skipping etc is handled at StreamAudio() so no need to anything else here

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message == null ? $"[ERROR]: {ex.Message}" : $"[ERROR]: Something went wrong in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }

            return;
        }

        private async Task AcknowledgeCommand(SocketSlashCommand command)
        {
            // Acknowledge slashcommand before response time runs out
            await command.DeferAsync();

            // Create or update cancellationtokensource for a guild
            if (command.GuildId is ulong guildId)
            {
                UpdateOrAddCancellationTokenSource(guildId);
            }

            return;
        }

        private CancellationTokenSource UpdateOrAddCancellationTokenSource(ulong guildId, CancellationTokenSource? oldcTokenSource = null)
        {
            CancellationTokenSource newcTokenSource = new();

            if (oldcTokenSource == null)
            {
                _guildcTokenSources.AddOrUpdate(guildId, newcTokenSource, (key, oldValue) => oldValue);
            }
            else
            {
                _guildcTokenSources.TryUpdate(guildId, newcTokenSource, oldcTokenSource);
            }

            return newcTokenSource;
        }

        private async Task AppendQueryToQueueAsync(SocketSlashCommand command)
        {
            // Create song object with slashcommands first parameter
            Song song = _ytDlp.GetSongFromSlashCommand(command);

            if (command.GuildId is ulong guildId)
            {
                _guildQueues.GetOrAdd(guildId, _ => new List<Song>()).Add(song);

                // Don't inform of user request if queue is empty
                if (_guildQueues.TryGetValue((ulong)command.GuildId!, out var songList) ? songList.Count > 1 : false)
                {
                    await ConstructResponses("user-requested", command);
                }
            }

            return;
        }

        private Task StreamAudio(SocketSlashCommand command, IAudioClient audioClient)
        {
            if (command.GuildId is not ulong guildId)
            {
                throw new Exception($"Command GuildID was null at {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }

            var songList = _guildQueues.TryGetValue(guildId, out var foundList) ? foundList : null;
            var cTokenSource = _guildcTokenSources.TryGetValue(guildId, out var foundcTokenSource) ? foundcTokenSource : null;

            if (songList == null || songList.Count >= 2 || cTokenSource == null)
            {
                return Task.CompletedTask;
            }

            Task streamAudioTask = new Task(async () =>
            {
                while (songList.Count > 0)
                {
                    using (var ffmpegStream = _ffmpeg.GetAudioStreamFromUrl(songList[0].AudioUrl))
                    using (var outStream = audioClient.CreatePCMStream(AudioApplication.Music, bufferMillis: 1000))
                    {
                        try
                        {
                            // Display what we're playing in a response
                            await ConstructResponses("now-playing", command);

                            // Set firstSong boolean to false
                            _guildFirstSong.TryUpdate(guildId, false, true);

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
                            cTokenSource = UpdateOrAddCancellationTokenSource(guildId, cTokenSource);
                        }
                        finally
                        {
                            // Flush the audio stream 
                            await outStream.FlushAsync();

                            // Remove the song we just played from the song list
                            songList.RemoveAt(0);

                            // Dispose stream to save resources
                            ffmpegStream.Dispose();

                            // Check if songlist is empty. Set firstSong to true if thats the case
                            if (songList.Count <= 0)
                            {
                                _guildFirstSong.TryUpdate(guildId, true, false);
                            }
                        }

                    }
                }
            }, cTokenSource.Token);

            streamAudioTask.Start();

            return Task.CompletedTask;
        }

        private async Task ConstructResponses(string type, SocketSlashCommand? command = null, SocketMessageComponent? component = null)
        {
            // This method assumes we either get SocketSlashCommand or SocketMessageComponent.
            // Both of them cannot have values or be null at the same time

            if ((command == null && component == null) || (command != null && component != null))
            {
                throw new Exception($"Both SocketSlashCommand and SocketMessageComponent were null or had values in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }

            List<Song> songList;

            var embedBuilder = new EmbedBuilder();
            var componentBuilder = new ComponentBuilder();

            if (command != null && command.GuildId is ulong commandGuildId && command.User is IGuildUser commandUser)
            {
                // Construct error responses here. Succesful responses are handled in a separate switch tree
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
                            case "channel-not-found":
                                embedBuilder.Description = embedBuilder.Description + $"\n\nIf you believe this is a bug, please fill out a bug report at the developers discord server.";
                                break;
                            case "channel-full":
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

                songList = _guildQueues.TryGetValue(commandGuildId, out var list) && list.Any() ? list : new List<Song>();

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
                        embedBuilder.Title = songList[songList.Count - 1].Title;
                        embedBuilder.Url = songList[songList.Count - 1].VideoUrl;
                        embedBuilder.ThumbnailUrl = songList[songList.Count - 1].ThumbnailUrl;
                        embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = commandUser.GlobalName, IconUrl = commandUser.GetAvatarUrl() });

                        if (songList.Count > 2)
                        {
                            if (embedBuilder.Fields == null || embedBuilder.Fields.Count <= 0)
                            {
                                embedBuilder.Fields = new List<EmbedFieldBuilder>
                            {
                                new EmbedFieldBuilder
                                {
                                    Name = "Songs in the queue",
                                    Value = $"- {songList[1].Title} \n",
                                }
                            };
                            }

                            for (int i = 2; i != songList.Count; i++)
                            {
                                embedBuilder.Fields[0].Value += $"- {songList[i].Title} \n";
                            }
                        }

                        await command.ModifyOriginalResponseAsync(msg =>
                        {
                            msg.Embeds = new[] { embedBuilder.Build() };
                            msg.Components = componentBuilder.Build();
                        });
                        break;
                    case "now-playing":
                        // Configure embedBuilder
                        embedBuilder.Author = new EmbedAuthorBuilder
                        {
                            IconUrl = null,
                            Name = commandUser.VoiceChannel == null ? "Now playing" : $"Now playing in {commandUser.VoiceChannel}",
                            Url = null
                        };
                        embedBuilder.Title = songList[0].Title;
                        embedBuilder.Url = songList[0].AudioUrl;
                        embedBuilder.Fields = new List<EmbedFieldBuilder>
                        {
                            new EmbedFieldBuilder
                            {
                                Name = "Duration" ,
                                Value = $"{songList[0].Duration.Hours:D2}:{songList[0].Duration.Minutes:D2}",
                                IsInline = true
                            },
                            new EmbedFieldBuilder
                            {
                                Name = "Songs in queue" ,
                                Value = $"{songList.Count - 1}",
                                IsInline = true
                            }
                        };
                        embedBuilder.ImageUrl = songList[0].ThumbnailUrl;
                        embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = commandUser.GlobalName, IconUrl = commandUser.GetAvatarUrl() });

                        // Add "Next song" field if we have more than 1 song in queue
                        if (songList.Count > 1)
                        {
                            embedBuilder.Fields.Add(new EmbedFieldBuilder
                            {
                                Name = "Next in queue",
                                Value = $"{songList[1].Title ?? "Title"}",
                                IsInline = false
                            });
                        };

                        // Configure componentBuilder
                        componentBuilder.WithButton("Skip", "skip-song-button");

                        bool firstSong = _guildFirstSong.TryGetValue(commandGuildId, out var foundFirstSong) ? foundFirstSong : true;

                        // Send "Now playing" as independend message if
                        // - Songlist has 2 or more songs
                        // - firstSong is false
                        if (songList.Count > 1 || firstSong == false)
                        {
                            await command.Channel.SendMessageAsync(embeds: [embedBuilder.Build()], components: componentBuilder.Build());
                        }
                        else
                        {
                            await command.ModifyOriginalResponseAsync(msg =>
                            {
                                msg.Embeds = new[] { embedBuilder.Build() };
                                msg.Components = componentBuilder.Build();
                            });
                        }
                        break;
                    default:
                        break;

                }

                return;
            }
            else if (component != null && component.GuildId is ulong componentGuildId && component.User is IGuildUser componentUser)
            {
                songList = _guildQueues.TryGetValue(componentGuildId, out var list) && list.Any() ? list : new List<Song>();

                switch (type)
                {
                    case "song-skipped":
                        embedBuilder.Author = new EmbedAuthorBuilder
                        {
                            IconUrl = null,
                            Name = $"Song skipped",
                            Url = null
                        };
                        embedBuilder.Title = songList[0].Title;
                        embedBuilder.Url = songList[0].AudioUrl;
                        embedBuilder.ThumbnailUrl = songList[0].ThumbnailUrl;
                        embedBuilder.WithDefaults(new EmbedFooterBuilder { Text = componentUser.GlobalName, IconUrl = componentUser.GetAvatarUrl() });

                        await component.RespondAsync(embeds: new[] { embedBuilder.Build() });
                        break;
                    default:
                        break;
                }

                return;
            }

            return;
        }

        private Task StreamDestroyed(ulong streamId)
        {
            // Purge all resources tied to the guild
            if (_guildStreams.TryGetValue(streamId, out var guildId))
            {
                _guildStreams.TryRemove(streamId, out _);
            }

            if (_guildQueues.TryGetValue(guildId, out var songList))
            {
                _guildQueues.TryRemove(guildId, out _);
            }

            if (_guildFirstSong.TryGetValue(guildId, out var firstSong))
            {
                _guildFirstSong.TryRemove(guildId, out _);
            }

            if (_guildAudioClients.TryGetValue(guildId, out var audioClient))
            {
                _guildAudioClients.TryRemove(guildId, out _);
            }

            if (_guildcTokenSources.TryGetValue(guildId, out var cTokenSource))
            {
                _guildcTokenSources.TryRemove(guildId, out _);
            }

            return Task.CompletedTask;
        }


    }
}
