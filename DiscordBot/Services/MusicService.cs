using Discord;
using Discord.Audio;
using Discord.WebSocket;
using DiscordBot.Modules.Interfaces;
using DiscordBot.Services.Interfaces;
using System.IO;

namespace DiscordBot.Services
{
    public class MusicService : IMusicService
    {
        private IYtDlp _ytDlp;
        private IFFmpeg _ffmpeg;

        private SocketSlashCommand? _command;
        private IVoiceChannel? _channel;
        private IAudioClient? _audioClient;
        private ConnectionState _connectionState;

        private List<string> _audioQueue;
        private List<string> _displayQueue;


        public MusicService(IYtDlp ytDlp, IFFmpeg ffmpeg)
        {
            _ytDlp = ytDlp ?? throw new NullReferenceException(nameof(ytDlp));
            _ffmpeg = ffmpeg ?? throw new NullReferenceException(nameof(ffmpeg));

            _audioQueue = new List<string>();
            _displayQueue = new List<string>();
        }

        public async Task Play(SocketSlashCommand command)
        {
            _command = command;

            if (_command.User is not IGuildUser user)
            {
                return;
            }

            _channel = user.VoiceChannel;

            // Join if bot is in disconnected state
            if (_connectionState == ConnectionState.Disconnected && _channel != null)
            {
                _audioClient = await _channel.ConnectAsync(true, false, false, false);
                _audioClient.Connected += ClientConnected;
                _audioClient.StreamCreated += StreamCreated;
                _audioClient.ClientDisconnected += ClientDisconnected;

                _connectionState = _audioClient.ConnectionState;

                if (_connectionState != ConnectionState.Connected)
                {
                    await Respond("connecting-failed");
                }
            }

            // Since bot is already connected, we can manually move to ClientConnected() method
            await ClientConnected();
        }

        private async Task ClientConnected()
        {
            // Extract url from slashcommands first parameter
            string? url = _command!.Data.Options.First().Value.ToString();

            if (!String.IsNullOrEmpty(url))
            {
                // Add parameter to display queue for user feedback
                _displayQueue.Add(url);

                // Respond to slashcommand with the requested url and other basic information
                await Respond("connected");

                // Extract audio url from link and add it to audio queue
                string audioUrl = _ytDlp.GetAudioUrlFromLink(url);

                _audioQueue.Add(audioUrl);
            }
            else
            {
                return;
            }

            // Start streaming audio
            using (var output = _ffmpeg.GetAudioStreamFromUrl(_audioQueue.First()))
            using (var discord = _audioClient.CreatePCMStream(AudioApplication.Mixed))
            {
                try { await output.CopyToAsync(discord); }
                finally { await discord.FlushAsync(); }
            }
        }

        private Task ClientDisconnected(ulong channelId)
        {
            // Clear queue and set connectionstate to disconnected
            _audioQueue.Clear();

            _connectionState = ConnectionState.Disconnected;

            // Dispose audio client
            if (_audioClient != null)
            {
                _audioClient.Dispose();
            }

            return Task.CompletedTask;
        }

        private Task StreamCreated(ulong number, AudioInStream stream)
        {
            var test1 = stream;
            var test2 = number;

            return Task.CompletedTask;
        }

        private async Task Respond(string type)
        {
            string message = "";

            switch (type)
            {
                case "connecting-failed":
                    if (_command != null)
                    {
                        message = $"{_command.User.Mention} try joining a voice channel before requesting /play -_-";
                    }
                    break;
                case "connected":
                    message = $"Now playing {_displayQueue.FirstOrDefault()}";
                    break;
            }

            if (_command != null)
            {
                await _command.RespondAsync(message);
            }

            return;
        }


    }
}
