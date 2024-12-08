using Discord;
using Discord.Audio;
using Discord.WebSocket;
using DiscordBot.Extensions;
using DiscordBot.Models;
using DiscordBot.Modules.Interfaces;
using DiscordBot.Repositories.Interfaces;
using DiscordBot.Services.Interfaces;
using System.Reflection;

namespace DiscordBot.Services
{
    public class MusicService : IMusicService
    {
        private IConfigurationRepository _configurationRepository;

        private IYtDlp _ytDlp;
        private IFFmpeg _ffmpeg;

        private SocketSlashCommand? _command;
        private IVoiceChannel? _channel;
        private IAudioClient? _audioClient;

        private List<Song> _songList;

        private bool _firstSong;

        public MusicService(IConfigurationRepository configurationRepository, IYtDlp ytDlp, IFFmpeg ffmpeg)
        {
            _configurationRepository = configurationRepository ?? throw new NullReferenceException(nameof(configurationRepository));

            _ytDlp = ytDlp ?? throw new NullReferenceException(nameof(ytDlp));
            _ffmpeg = ffmpeg ?? throw new NullReferenceException(nameof(ffmpeg));

            _songList = new List<Song>();

            _firstSong = true;
        }

        // ToDo
        // - Buttons for embeds
        // - Better error logic
        // - Fix playback

        public async Task Play(SocketSlashCommand command)
        {
            try
            {
                _command = command;

                if (command.User is not IGuildUser user)
                {
                    throw new Exception($"> [ERROR]: SocketSlashCommand.User was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                }

                _channel = user.VoiceChannel;

                if (_channel != null)
                {
                    // Execution order and logic:
                    // 1. Make sure channel has room for the bot
                    // 2. Check if we're already connected to a channel. Skip to next step if we have a valid IAudioClient
                    // 3. Connect to channel and create an IAudioClient. Throw an error if there was an issue

                    if (_channel.UserLimit <= _channel.GetUsersAsync().CountAsync().Result)
                    {
                        // Use Ephemeral response so other users don't see the issue
                        await ConstructResponses("channel-full");

                        return;
                    }
                    else if (_audioClient != null && _audioClient.ConnectionState == ConnectionState.Connected)
                    {
                        await AppendQueryToQueueAsync();

                        return;
                    }
                    else if (_audioClient == null || _audioClient.ConnectionState == ConnectionState.Disconnected)
                    {
                        _audioClient = await _channel.ConnectAsync(true, false, false, false);
                        _audioClient.StreamDestroyed += StreamDestroyedAsync;

                        if (_audioClient == null || _audioClient.ConnectionState == ConnectionState.Disconnected)
                        {
                            throw new Exception($"> [ERROR]: Unable to create IAudioClient with IVoiceChannel.ConnectAsync in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                        }

                        await AppendQueryToQueueAsync();
                    }

                }
                else
                {
                    // Inform user with an ephemeral response that they should be in a voice channel before requesting /play
                    await ConstructResponses("channel-not-found");

                    return;
                }
            }
            catch (Exception ex)
            {
                // Reset "global" variables
                _songList.Clear();
                _firstSong = true;

                Console.WriteLine(ex.Message ?? $"[ERROR]: Something went wrong in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }

        }

        private async Task AppendQueryToQueueAsync()
        {
            // Extract slash commands first parameter and add it to query list
            if (_command is not SocketSlashCommand command || command.Data.Options.First().Value.ToString() is not string query)
            {
                throw new Exception($"> [ERROR]: SocketSlashCommands first parameter was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }

            // After connecting to a voice channel, inform discord that we acknowledge the slash command
            await _command.DeferAsync();

            // Extract audio uri from the link, pre-create it and add it to the list
            var song = _ytDlp.GetSongFromQuery(query);

            if (String.IsNullOrEmpty(song.AudioUrl))
            {
                throw new Exception($"> [ERROR]: Couldn't fetch an audio url with yt-dlp in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }

            song.Requester = _command.User;
            _songList.Add(song);

            // Provide response to the slash command
            await ConstructResponses("user-requested");

            // Prevent the streaming of multiple URI's at once
            if (_songList.Count <= 1 && _audioClient is IAudioClient audioClient)
            {
                await StreamAudio(audioClient);
            }

        }

        private async Task StreamAudio(IAudioClient audioClient)
        {
            while (_songList.Count > 0)
            {
                await ConstructResponses("now-playing");

                using (var ffmpegStream = _ffmpeg.GetAudioStreamFromUrl(_songList[0]))
                using (var outStream = audioClient.CreatePCMStream(AudioApplication.Music, bufferMillis: 1000))
                {
                    try
                    {
                        _firstSong = false;

                        // Write to outStream in pieces rather than all at once
                        byte[] buffer = new byte[8192];
                        int bytesRead;

                        while ((bytesRead = await ffmpegStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await outStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        }
                    }
                    catch
                    {
                        throw new Exception($"> [ERROR]: Exception thrown while streaming music in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                    }
                    finally
                    {
                        // Remove the song we just played from the song list
                        _songList.RemoveAt(0);

                        // Flush the audio stream 
                        await outStream.FlushAsync();

                        // Dispose the stream to create a new one
                        ffmpegStream.Dispose();
                    }

                }
            }

            return;
        }

        private async Task ConstructResponses(string type)
        {
            if (_command is not SocketSlashCommand command)
            {
                return;
            }

            var builder = new EmbedBuilder();

            switch (type)
            {
                case "user-requested":
                    // Don't inform about the first song being added to queue
                    if (_firstSong)
                    {
                        return;
                    }

                    builder.Author = new EmbedAuthorBuilder
                    {
                        IconUrl = "",
                        Name = $"Added a new song to the queue",
                        Url = ""
                    };
                    builder.Title = _songList[_songList.Count - 1].Title;
                    builder.Url = _songList[_songList.Count - 1].VideoUrl;
                    builder.ThumbnailUrl = _songList[_songList.Count - 1].ThumbnailUrl;
                    builder.WithDefaults(new EmbedFooterBuilder { Text = _command.User.GlobalName, IconUrl = _command.User.GetAvatarUrl() });

                    if (_songList.Count > 2)
                    {
                        if (builder.Fields == null || builder.Fields.Count <= 0)
                        {
                            builder.Fields = new List<EmbedFieldBuilder>
                            {
                                new EmbedFieldBuilder
                                {
                                    Name = "Songs in the queue",
                                    Value = $"- {_songList[1].Title} \n",
                                }
                            };
                        }

                        for (int i = 2; i != _songList.Count; i++)
                        {
                            builder.Fields[0].Value += $"- {_songList[i].Title} \n";
                        }
                    }

                    await command.ModifyOriginalResponseAsync(msg =>
                    {
                        msg.Embeds = new[] { builder.Build() };
                    });
                    break;
                case "now-playing":
                    builder.Author = new EmbedAuthorBuilder
                    {
                        IconUrl = null,
                        Name = _channel == null ? "Now playing" : $"Now playing in {_channel.Name}",
                        Url = null
                    };
                    builder.Title = _songList[0].Title;
                    builder.Url = _songList[0].VideoUrl;
                    builder.Fields = new List<EmbedFieldBuilder>
                    {
                        new EmbedFieldBuilder
                        {
                            Name = "Duration" ,
                            Value = $"{_songList[0].Duration.Hours:D2}:{_songList[0].Duration.Minutes:D2}",
                             IsInline = true
                        },
                        new EmbedFieldBuilder
                        {
                            Name = "Songs in queue" ,
                            Value = $"{_songList.Count - 1}",
                                IsInline = true
                        }
                    };
                    builder.ImageUrl = _songList[0].ThumbnailUrl;

                    // Add "Next song" field if we have more than 1 song in queue
                    if (_songList.Count > 1)
                    {
                        builder.Fields.Add(new EmbedFieldBuilder
                        {
                            Name = "Next in queue",
                            Value = $"{_songList[1].Title ?? "Title"}",
                            IsInline = false
                        });
                    };
                    builder.WithDefaults(new EmbedFooterBuilder { Text = _command.User.GlobalName, IconUrl = _command.User.GetAvatarUrl() });

                    if (!_firstSong)
                    {
                        await command.Channel.SendMessageAsync(embeds: [builder.Build()]);
                    }
                    else
                    {
                        await command.ModifyOriginalResponseAsync(msg =>
                        {
                            msg.Embeds = new[] { builder.Build() };
                        });
                    }
                    break;
            }

            // Return if type isn't an error
            if (type is not ("error" or "channel-not-found" or "channel-full"))
            {
                return;
            }

            string discordLink = _configurationRepository.GetDiscordLink();

            switch (type)
            {
                case "error":
                    builder.Title = $"Something went wrong :(";
                    builder.Description = $"Something went wrong while executing **/{command.CommandName}**. ";
                    break;
                case "channel-not-found":
                    builder.Title = $"Couldn't connect to a voice channel";
                    builder.Description = $"You should be connected to a voice channel before requesting **/{command.CommandName}**. ";
                    break;
                case "channel-full":
                    builder.Title = $"Couldn't connect to the voice channel";
                    builder.Description = $"The voice channel is at maximum capacity. You could kick your least favorite friend to make room.";
                    break;
            }

            // Add developer details if its configured in appsettings.json
            if (!String.IsNullOrEmpty(discordLink))
            {
                if (type == "error")
                {
                    builder.Description = builder.Description + $"Please fill out a bug report at the developers discord server. ";
                }
                else
                {
                    builder.Description = builder.Description + $"\n\nIf you believe this is a bug, please fill out a bug report at the developers discord server.";
                }

                builder.Fields.Add(new EmbedFieldBuilder
                {
                    Name = $"Discord server",
                    Value = discordLink,
                    IsInline = true
                });
            }

            builder.WithDefaults(new EmbedFooterBuilder { Text = _command.User.GlobalName, IconUrl = _command.User.GetAvatarUrl() });

            await command.RespondAsync(embeds: [builder.Build()], ephemeral: true);

            return;
        }

        private Task StreamDestroyedAsync(ulong streamId)
        {
            _firstSong = true;

            // Clear lists
            _songList.Clear();

            // Dispose IAudioClient
            if (_audioClient is IAudioClient audioClient)
            {
                audioClient.Dispose();
            }

            return Task.CompletedTask;
        }


    }
}
