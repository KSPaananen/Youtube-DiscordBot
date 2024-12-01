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

        private string? _link;
        private bool alreadyJoined;

        public Voice()
        {

        }

        public async Task Play(SocketSlashCommand command, bool isRetry)
        {
            try
            {
                // Extract link which is parameter in /play command
                _link = command.Data.Options.First().Value.ToString();

                bool joinSuccess;

                // Use RetryJoin() if parameter isRetry is true
                switch (isRetry)
                {
                    case false:
                        joinSuccess = await Join(command);
                        break;
                    case true:
                        joinSuccess = await RetryJoin(command);
                        break;
                }

                if (!joinSuccess)
                {
                    return;
                }

                // Provide a response to slash command
                await Respond(command);

                // Start streaming audio
                await StreamAudio();

            }
            catch (Exception ex)
            {
                await command.Channel.SendMessageAsync($"Something went wrong -_-");
                Console.WriteLine(ex.Message);
            }
        }

        private async Task<bool> Join(SocketSlashCommand command)
        {
            var guildUser = (command.User as IGuildUser);

            if (guildUser == null)
            {
                return false;
            }

            _channel = guildUser.VoiceChannel;

            // Tell user to join a voice channel if they're not in one
            if (_channel == null)
            {
                ComponentBuilder builder = new ComponentBuilder().WithButton("Retry", "id-retry-playlink-button");

                await command.RespondAsync($"{command.User.Mention} try joining a voice channel before requesting /play -_-", components: builder.Build());

                return false;
            }

            _audioClient = await _channel.ConnectAsync();

            return true;
        }

        private async Task<bool> RetryJoin(SocketSlashCommand command)
        {
            var guildUser = (command.User as IGuildUser);

            if (guildUser == null)
            {
                return false;
            }

            _channel = guildUser.VoiceChannel;

            if (_channel == null)
            {
                await command.DeleteOriginalResponseAsync();

                return false;
            }

            _audioClient = await _channel.ConnectAsync();

            return true;
        }

        private Task StreamAudio()
        {
            return Task.CompletedTask;
        }

        private async Task Respond(SocketSlashCommand command)
        {
            // Create buttons
            // Pause icon ⏸
            ComponentBuilder builder = new ComponentBuilder()
                .WithButton("⏮", "id-rewind-song-button")
                .WithButton("⏯", "id-play-pause-song-button")
                .WithButton("⏭", "id-skip-song-button");

            await command.Channel.SendMessageAsync($"Now playing {_link}", components: builder.Build());

        }


    }
}

