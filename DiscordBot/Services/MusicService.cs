using Discord;
using Discord.Audio;
using Discord.WebSocket;
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

        private List<string> _urlList;

        private bool _firstSong;
        private bool _responded;

        public MusicService(IYtDlp ytDlp, IFFmpeg ffmpeg)
        {
            _ytDlp = ytDlp ?? throw new NullReferenceException(nameof(ytDlp));
            _ffmpeg = ffmpeg ?? throw new NullReferenceException(nameof(ffmpeg));

            _urlList = new List<string>();

            _firstSong = true;
        }

        public async Task Play(SocketSlashCommand command)
        {
            try
            {
                _responded = false;
                _command = command;

                // Tell discord that we acknowledge the slash command, giving us more time to respond
                await _command.DeferAsync();

                if (_command is not SocketSlashCommand || command.User is not IGuildUser user)
                {
                    throw new Exception($"[ERROR]: SocketSlashCommand was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
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
                        await Respond("channel-full", true);

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
                    await Respond("channel-not-found", true);

                    return;
                }
            }
            catch (Exception ex)
            {
                // Respond to slash command that something went wrong & print exception to console
                await Respond("error", true);

                Console.WriteLine(ex.Message ?? $"[ERROR]: Something went wrong in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");

                return;
            }

        }

        private async Task AppendQueryToQueueAsync()
        {
            // Extract slash commands first parameter and add it to query list
            if (_command is not SocketSlashCommand command || command.Data.Options.First().Value.ToString() is not string query)
            {
                throw new Exception($"[ERROR]: SocketSlashCommands first parameter was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }

            // Extract audio uri from the link, pre-create it and add it to the list
            string audioUrl = _ytDlp.GetAudioUrlFromQuery(query);

            if (String.IsNullOrEmpty(audioUrl))
            {
                throw new Exception($"[ERROR]: Couldn't fetch an audio url with yt-dlp in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }

            _urlList.Add(audioUrl);

            // Provide response to the slash command
            await Respond("user-requested", true);

            // Prevent the streaming of multiple URI's at once
            if (_urlList.Count <= 1 && _audioClient is IAudioClient audioClient)
            {
                await StreamAudio(audioClient);
            }

        }

        private async Task StreamAudio(IAudioClient audioClient)
        {
            // Allow first songs some buffertime to prevent cutouts
            int bufferMillis = _firstSong ? 1000 : 500;

            while (_urlList.Count > 0)
            {
                // Send information about the song we're playing to text channel
                await Respond("now-playing", false);

                using (var ffmpegStream = _ffmpeg.GetAudioStreamFromUrl(_urlList[0]))
                using (var outStream = audioClient.CreatePCMStream(AudioApplication.Music, bufferMillis: bufferMillis))
                {
                    try
                    {
                        _firstSong = false;

                        // Write to outStream in pieces rather than all at once
                        byte[] buffer = new byte[2048];
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
                        //ffmpegStream.Dispose();

                        // Remove the song we just played from the queue
                        _urlList.RemoveAt(0);
                    }

                }
            }

            return;
        }

        private async Task Respond(string type, bool response)
        {
            if (_command is not SocketSlashCommand command)
            {
                return;
            }

            string message = "";

            switch (type)
            {
                case "error":
                    message = $"Something went wrong. Please fill a bug report in discord.gg/myserverlink.";

                    break;
                case "channel-full":
                    message = $"{_command.User.Mention} Voice channel at maximum capacity";

                    break;
                case "channel-not-found":
                    message = $"Join a voice channel before requesting /play";

                    break;
                case "user-requested":
                    message = $"{_command.Data.Options.First().Value.ToString()} added to the queue";

                    break;
                case "now-playing":
                    message = $"Now playing {_urlList.First()}";

                    break;
                default:
                    break;
            }

            if (response)
            {
                await command.ModifyOriginalResponseAsync(msg =>
                {
                    msg.Content = "";
                    msg.Embed = new EmbedBuilder()
                        .WithTitle("Song added to the queue")
                        .WithDescription(message)
                        .Build();
                });
            }
            else
            {
                await _command.Channel.SendMessageAsync(message);
            }

            return;
        }

        private Task StreamDestroyedAsync(ulong esfse)
        {
            // Set firstSong back to true
            _firstSong = true;

            // Clear lists
            _urlList.Clear();

            // Dispose IAudioClient
            if (_audioClient is IAudioClient audioClient)
            {
                audioClient.Dispose();
            }

            return Task.CompletedTask;
        }


    }
}
