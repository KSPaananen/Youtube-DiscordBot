using Discord;
using Discord.Audio;
using Discord.WebSocket;
using DiscordBot.Modules.Interfaces;
using DiscordBot.Services.Interfaces;
using System.IO;

namespace DiscordBot.Services
{
    public class VoiceService : IVoiceService
    {
        private IAudio _audio;

        private IVoiceChannel? _channel;
        private IAudioClient? _audioClient;

        private List<string> _queue;
        private ConnectionState _connectionState;

        public VoiceService(IAudio audio)
        {
            _audio = audio ?? throw new NullReferenceException(nameof(audio));

            _queue = new List<string>();
        }

        public async Task Play(SocketSlashCommand command)
        {
            // Try joining voice chat if not already connected
            if (_connectionState == ConnectionState.Disconnected)
            {
                _queue.Clear();

                bool joinSuccess = await Join(command);

                if (!joinSuccess)
                {
                    // Provide response
                    await Respond(command, "notify-not-in-voice-chat");

                    return;
                }
            }

            // Add song to queue
            bool addToQueueSuccess = await AddSongToQueue(command);

            if (!addToQueueSuccess)
            {
                // Respond method
                await Respond(command, "notify-error-adding-to-queue");

                // Disconnect from voice chat
                await Disconnect(command);

                return;
            }

            // Start streaming audio
            bool streamingSuccees = await StreamAudio(command);





        }

        public async Task ListQueue(SocketSlashCommand command)
        {
            try
            {
                await Respond(command, "notify-queue-listed");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public async Task ClearQueue(SocketSlashCommand command)
        {
            try
            {
                _queue.Clear();

                await Respond(command, "notify-queue-cleared");

                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                return;
            }
        }

        private async Task<bool> Join(SocketSlashCommand command)
        {
            try
            {
                var guildUser = command.User as IGuildUser;

                if (guildUser == null)
                {
                    return false;
                }

                _channel = guildUser.VoiceChannel;

                // Return false if no channel is present
                if (_channel == null)
                {
                    return false;
                }

                // Connect to voicechat
                _audioClient = await _channel.ConnectAsync();

                if (_audioClient.ConnectionState == ConnectionState.Connecting)
                {
                    Thread.Sleep(1000);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                _queue.Clear();

                return false;
            }
        }

        private async Task<bool> Disconnect(SocketSlashCommand command)
        {
            try
            {
                if (_channel != null)
                {
                    await _channel.DisconnectAsync();
                }
                else
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                return false;
            }
        }

        private Task<bool> AddSongToQueue(SocketSlashCommand command)
        {
            try
            {
                string? firstParam = command.Data.Options.First().Value.ToString();

                if (firstParam == null || firstParam == "")
                {
                    return Task.FromResult(false);
                }

                // Extract audio uri from link
                string audioUri = _audio.GetAudioUrlFromLink(firstParam);

                if (audioUri == "")
                {
                    return Task.FromResult(false);
                }

                _queue.Add(audioUri);

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                return Task.FromResult(false);
            }
        }

        private async Task<bool> StreamAudio(SocketSlashCommand command)
        {
            try
            {
                // Get the first link from queue
                string link = _queue[0];

                if (link == null || link == "")
                {
                    return false;
                }

                // Respond to command
                await Respond(command, "notify-playing");

                using (var ffmpeg = _audio.GetAudioStreamFromUrl(link))
                using (var output = ffmpeg.StandardOutput.BaseStream)
                using (var discord = _audioClient.CreatePCMStream(AudioApplication.Music))
                {
                    try
                    {
                        var buffer = new byte[1024];  // You might need to adjust the buffer size
                        int bytesRead;
                        while ((bytesRead = await output.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await discord.WriteAsync(buffer, 0, bytesRead);
                        }
                    }
                    finally
                    {
                        await discord.FlushAsync();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                return false;
            }
        }

        private async Task<bool> Respond(SocketSlashCommand command, string type)
        {
            try
            {
                string message = "";

                switch (type)
                {
                    case "notify-playing":
                        var notifyPlayingBuilder = new ComponentBuilder()
                            .WithButton("Rewind", "id-rewind-song-button")
                            .WithButton("Stop playing", "id-stop-playing-button")
                            .WithButton("Skip", "id-skip-song-button");

                        if (_queue.Count() > 0)
                        {
                            message = $"Now playing {_queue.First()}";
                        }
                        else
                        {
                            message = $"Queue empty";
                        }

                        await command.RespondAsync(message, components: notifyPlayingBuilder.Build());
                        break;
                    case "notify-not-in-voice-chat":
                        message = $"{command.User.Mention} try joining a voice channel before requesting /play -_-";

                        await command.RespondAsync(message);
                        break;
                    case "notify-error-adding-to-queue":
                        message = $"Error adding song to queue";

                        await command.RespondAsync(message);
                        break;
                    case "notify-queue-listed":
                        message = $"List of songs in the queue: \n";

                        for (int i = 0; i < _queue.Count(); i++)
                        {
                            message += $"- {_queue[i]} \n";
                        }

                        await command.RespondAsync(message);
                        break;
                    case "notify-queue-cleared":
                        message = $"Queue cleared";

                        await command.RespondAsync(message);
                        break;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                return false;
            }
        }


    }
}

