using Discord;
using Discord.Audio;
using Discord.WebSocket;
using DiscordBot.Modules.Interfaces;
using DiscordBot.Services.Interfaces;

namespace DiscordBot.Services
{
    public class MusicService : IMusicService
    {
        private IYtDlp _ytDlp;
        private IFFmpeg _ffmpeg;

        private SocketSlashCommand? _command;
        private IVoiceChannel? _channel;
        private IAudioClient? _audioClient;

        private List<string> _videoUrlQueue;
        private List<string> _audioUrlQueue;

        private bool _firstSong;

        public MusicService(IYtDlp ytDlp, IFFmpeg ffmpeg)
        {
            _ytDlp = ytDlp ?? throw new NullReferenceException(nameof(ytDlp));
            _ffmpeg = ffmpeg ?? throw new NullReferenceException(nameof(ffmpeg));

            _videoUrlQueue = new List<string>();
            _audioUrlQueue = new List<string>();

            _firstSong = true;
        }

        public async Task Play(SocketSlashCommand command)
        {
            try
            {
                _command = command;

                if (_command.User is not IGuildUser user)
                {
                    throw new Exception("[ERROR]: SocketSlashCommand.User was null in MusicService.cs : Play()");
                }

                _channel = user.VoiceChannel;

                if (_channel != null)
                {
                    // Skip connecting if we have an active audio client and user is in a channel
                    if (_audioClient != null && _audioClient.ConnectionState == ConnectionState.Connected)
                    {
                        await AppendURIsToQueueAsync();

                        return;
                    }
                    else if (_audioClient == null || _audioClient.ConnectionState == ConnectionState.Disconnected)
                    {
                        _audioClient = await _channel.ConnectAsync(true, false, false, false);
                        _audioClient.StreamDestroyed += StreamDestroyedAsync;

                        // Check if we were able to connect
                        if (_audioClient.ConnectionState == ConnectionState.Disconnected)
                        {
                            await Respond("connecting-failed");

                            return;
                        }

                        await AppendURIsToQueueAsync();
                    }
                    else if (_channel.UserLimit <= _channel.GetUsersAsync().CountAsync().Result)
                    {
                        // Respond & inform user that there are too many users in the channel
                        await Respond("channel-full");

                        return;
                    }
                }
                else
                {
                    // Respond & inform user to connect to a voice chat before requesting play
                    await Respond("channel-not-found");

                    return;
                }

            }
            catch (Exception ex)
            {
                string message = ex.Message ?? "[ERROR]: Something went wrong in MusicService : Play()";

                Console.WriteLine(message);

                return;
            }
        }

        private async Task AppendURIsToQueueAsync()
        {
            // Extract slash commands first parameter and add it to url queue
            if (_command is not SocketSlashCommand command || command.Data.Options.First().Value.ToString() is not string url)
            {
                return;
            }

            _videoUrlQueue.Add(url);

            // Respond to slashcommand with the requested url and other basic information
            await Respond("connected-succesfully");

            // Extract audio uri from the link, pre-create it and add it to the list
            string audioUrl = _ytDlp.GetAudioUrlFromLink(url);

            if (String.IsNullOrEmpty(audioUrl))
            {
                await Respond("couldnt-extract-audio-url");

                return;
            }

            _audioUrlQueue.Add(audioUrl);

            // Prevent the streaming of multiple URI's at once
            if (_audioUrlQueue.Count <= 1 && _audioClient is IAudioClient audioClient)
            {
                await StreamAudio(audioClient);
            }

        }

        private async Task StreamAudio(IAudioClient audioClient)
        {
            // Allow first songs some buffertime to prevent cutouts
            int bufferMillis = _firstSong ? 1000 : 500;

            while (_audioUrlQueue.Count > 0)
            {
                using (var ffmpegStream = _ffmpeg.GetAudioStreamFromUrl(_audioUrlQueue[0]))
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
                        throw new Exception("[ERROR]: Exception thrown while streaming music in MusicService : ClientConnected()");
                    }
                    finally
                    {
                        // Flush the audio stream 
                        await outStream.FlushAsync();

                        // Dispose the stream to create a new one
                        //ffmpegStream.Dispose();

                        // Remove the song we just played from the queue
                        _audioUrlQueue.RemoveAt(0);
                    }

                }
            }

            return;
        }

        private async Task Respond(string type)
        {
            if (_command is not SocketSlashCommand command)
            {
                return;
            }

            string message = "";

            switch (type)
            {
                case "connecting-failed":
                    message = $"{command.User.Mention} try joining a voice channel before requesting /play -_-";

                    break;
                case "connected-succesfully":
                    if (_firstSong) message = $"Now playing {_command.Data.Options.First().Value.ToString()}";
                    else message = $"Added to queue {_command.Data.Options.First().Value.ToString()}";

                    break;
                case "channel-not-found":
                    message = $"Join a voice channel before requesting /play";

                    break;
                case "channel-full":
                    message = $"{_command.User.Mention} Voice channel at maximum capacity";

                    break;
                case "couldnt-extract-audio-url":
                    message = $"Couldn't extract audio url from provided link";

                    break;
            }

            await command.RespondAsync(message);

            return;
        }

        private Task StreamDestroyedAsync(ulong esfse)
        {
            // Set firstSong back to true
            _firstSong = true;

            // Clear all queues
            _videoUrlQueue.Clear();
            _audioUrlQueue.Clear();

            // Dispose IAudioClient
            if (_audioClient is IAudioClient audioClient)
            {
                audioClient.Dispose();
            }

            return Task.CompletedTask;
        }


    }
}
