using Discord;
using Discord.Audio;
using Discord.WebSocket;
using DiscordBot.Models;
using DiscordBot.Modules.Interfaces;
using DiscordBot.Services.Interfaces;
using System.Reflection;

namespace DiscordBot.Services
{
    public class MusicService : IMusicService
    {
        private IYtDlp _ytDlp;
        private IFFmpeg _ffmpeg;

        private SocketSlashCommand? _command;
        private IVoiceChannel? _channel;
        private IAudioClient? _audioClient;

        private List<Song> _songList;

        private bool _firstSong;
        
        public MusicService(IYtDlp ytDlp, IFFmpeg ffmpeg)
        {
            _ytDlp = ytDlp ?? throw new NullReferenceException(nameof(ytDlp));
            _ffmpeg = ffmpeg ?? throw new NullReferenceException(nameof(ffmpeg));

            _songList = new List<Song>();

            _firstSong = true;
        }

        // ToDo
        // - Buttons for embeds
        // - Clean up logic for ProvideFeedbackAsync

        public async Task Play(SocketSlashCommand command)
        {
            try
            {
                _command = command;
                
                if (command.User is not IGuildUser user)
                {
                    throw new Exception($"[ERROR]: SocketSlashCommand.User was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
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
                        await ProvideFeedbackAsync("channel-full");

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
                            throw new Exception($"[ERROR]: Unable to create IAudioClient with IVoiceChannel.ConnectAsync in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                        }

                        await AppendQueryToQueueAsync();
                    }

                }
                else
                {
                    // Respond & inform user to connect to a voice chat before requesting play
                    await ProvideFeedbackAsync("channel-not-found");

                    return;
                }
            }
            catch (Exception ex)
            {
                // Reset "global" variables
                _songList.Clear();
                _firstSong = true;

                // Throw an exception and let SlashCommandHandler layer handle it
                throw new Exception(ex.Message);
            }

        }

        private async Task AppendQueryToQueueAsync()
        {
            // Extract slash commands first parameter and add it to query list
            if (_command is not SocketSlashCommand command || command.Data.Options.First().Value.ToString() is not string query)
            {
                throw new Exception($"[ERROR]: SocketSlashCommands first parameter was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }

            // After connecting to a voice channel, inform discord that we acknowledge the slash command
            await _command.DeferAsync();

            // Extract audio uri from the link, pre-create it and add it to the list
            var song = _ytDlp.GetSongFromQuery(query);

            if (String.IsNullOrEmpty(song.AudioUrl))
            {
                throw new Exception($"[ERROR]: Couldn't fetch an audio url with yt-dlp in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }

            song.Requester = _command.User;
            _songList.Add(song);

            // Provide response to the slash command
            await ProvideFeedbackAsync("user-requested");

            // Prevent the streaming of multiple URI's at once
            if (_songList.Count <= 1 && _audioClient is IAudioClient audioClient)
            {
                await StreamAudio(audioClient);
            }

        }

        private async Task StreamAudio(IAudioClient audioClient)
        {
            // Allow first songs some buffertime to prevent cutouts
            int bufferMillis = _firstSong ? 1000 : 500;

            while (_songList.Count > 0)
            {
                // Send a message to channel unless we're playing the first song, then it should be a response
                await ProvideFeedbackAsync("now-playing");

                using (var ffmpegStream = _ffmpeg.GetAudioStreamFromUrl(_songList[0].AudioUrl))
                using (var outStream = audioClient.CreatePCMStream(AudioApplication.Music, bufferMillis: bufferMillis))
                {
                    try
                    {
                        _firstSong = false;

                        // Write to outStream in pieces rather than all at once
                        byte[] buffer = new byte[4096];
                        int bytesRead;

                        while ((bytesRead = await ffmpegStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await outStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        }
                    }
                    catch
                    {
                        throw new Exception($"[ERROR]: Exception thrown while streaming music in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                    }
                    finally
                    {
                        // Flush the audio stream 
                        await outStream.FlushAsync();

                        // Dispose the stream to create a new one
                        ffmpegStream.Dispose();

                        // Remove the song we just played from the queue
                        _songList.RemoveAt(0);
                    }

                }
            }

            return;
        }

        private async Task ProvideFeedbackAsync(string type)
        {
            if (_command == null)
            {
                throw new Exception();
            }

            int listIndex = _songList.Count - 1;

            EmbedBuilder builder = new EmbedBuilder();
            builder.Color = new Color(1f, 0.984f, 0f);

            switch (type)
            {
                case "channel-full":
                    builder.Title = "Voice channel full";

                    await _command.RespondAsync(embeds: [builder.Build()], ephemeral: true);
                    break;
                case "channel-not-found":
                    builder.Title = "Couldn't connect to a voice channel";
                    builder.Description = "User must be in a voice channel before requesting /play";
                    builder.Timestamp = new DateTimeOffset(DateTime.Now);
                    builder.Footer = new EmbedFooterBuilder
                    {
                        IconUrl = _command!.User.GetAvatarUrl(),
                        Text = _command!.User.GlobalName
                    };

                    await _command.RespondAsync(embeds: [builder.Build()], ephemeral: true);

                    break;
                case "user-requested":
                    builder.Author = new EmbedAuthorBuilder
                    {
                        IconUrl = "",
                        Name = $"Added a new song to the queue",
                        Url = ""
                    };
                    builder.Title = _songList[listIndex].Title;
                    builder.Url = _songList[listIndex].VideoUrl;
                    builder.Description = "";
                    builder.Fields = new List<EmbedFieldBuilder>();
                    builder.ThumbnailUrl = _songList[listIndex].ThumbnailUrl;
                    builder.Timestamp = new DateTimeOffset(DateTime.Now);
                    builder.Footer = new EmbedFooterBuilder
                    {
                        IconUrl = _songList[0].Requester.GetAvatarUrl(),
                        Text = _songList[0].Requester.GlobalName
                    };

                    if (!_firstSong)
                    {
                        await _command.ModifyOriginalResponseAsync(msg =>
                        {
                            msg.Embeds = new[] { builder.Build() };
                        });
                    }

                    break;
                case "now-playing":
                    builder.Author = new EmbedAuthorBuilder
                    {
                        IconUrl = "",
                        Name = _channel == null ? "Now playing" : $"Now playing in {_channel.Name}",
                        Url = ""
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
                            Value = $"{listIndex}",
                            IsInline = true
                        },
                    };
                    builder.ImageUrl = _songList[0].ThumbnailUrl;
                    builder.Timestamp = new DateTimeOffset(DateTime.Now);
                    builder.Footer = new EmbedFooterBuilder
                    {
                        IconUrl = _songList[0].Requester.GetAvatarUrl(),
                        Text = _songList[0].Requester.GlobalName
                    };

                    // Add a field displaying the next song if we have songs in queue
                    if (_songList.Count >= 2)
                    {
                        builder.Fields.Add(new EmbedFieldBuilder
                        {
                            Name = "Next in queue",
                            Value = $"{_songList[1].Title ?? ""}",
                            IsInline = false
                        });
                    }

                    if (_firstSong)
                    {
                        await _command.ModifyOriginalResponseAsync(msg =>
                        {
                            msg.Embeds = new[] { builder.Build() };
                        });
                    }
                    else
                    {
                        await _command.Channel.SendMessageAsync(embeds: [builder.Build()]);
                    }

                    break;
                default:

                    break;
            }

            return;
        }

        private Task StreamDestroyedAsync(ulong streamId)
        {
            // Set firstSong back to true
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
