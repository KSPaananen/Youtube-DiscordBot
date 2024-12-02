using Discord;
using Discord.Audio;
using Discord.WebSocket;
using DiscordBot.Commands.Interfaces;

namespace DiscordBot.Commands
{
    public class Voice : IVoice
    {
        private IVoiceChannel? _channel;
        private IAudioClient? _audioClient;

        private List<string> _queue;
        private ConnectionState _connectionState;

        public Voice()
        {
            _queue = new List<string>();
        }

        public Task ClearQueue(SocketSlashCommand command)
        {
            try
            {
                _queue.Clear();

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                return Task.CompletedTask;
            }
        }

        // Entry method
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

        private async Task<bool> Join(SocketSlashCommand command)
        {
            try
            {
                var guildUser = (command.User as IGuildUser);

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
                // Add first parameter to queue
                if (command.Data.Options.First().Value.ToString() == null || command.Data.Options.First().Value.ToString() == "")
                {
                    return Task.FromResult(false);
                }

                _queue.Add(command.Data.Options.First().Value.ToString()!);

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

                // Start streaming audio
                // --
                // --

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
                switch (type)
                {
                    case "notify-playing":
                        var notifyPlayingBuilder = new ComponentBuilder()
                            .WithButton("⏮", "id-rewind-song-button")
                            .WithButton("⏯", "id-play-pause-song-button")
                            .WithButton("⏭", "id-skip-song-button");

                        if (_queue.Count() > 0)
                        {
                            await command.RespondAsync($"Now playing {_queue.First()}", components: notifyPlayingBuilder.Build());
                        }
                        else
                        {
                            await command.RespondAsync($"Queue empty");
                        }

                        break;
                    case "notify-not-in-voice-chat":
                        var notifyNotInVcBuilder = new ComponentBuilder().WithButton("Retry", "id-retry-playlink-button");

                        await command.RespondAsync($"{command.User.Mention} try joining a voice channel before requesting /play -_-", components: notifyNotInVcBuilder.Build());
                        break;
                    case "notify-error-adding-to-queue":
                        await command.RespondAsync($"Error adding song to queue");
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

      



        //private async Task<bool> RetryJoin(SocketSlashCommand command)
        //{
        //    var guildUser = (command.User as IGuildUser);

        //    if (guildUser == null)
        //    {
        //        return false;
        //    }

        //    _channel = guildUser.VoiceChannel;

        //    if (_channel == null)
        //    {
        //        await command.DeleteOriginalResponseAsync();

        //        return false;
        //    }

        //    _audioClient = await _channel.ConnectAsync();

        //    return true;
        //}

    }
}

