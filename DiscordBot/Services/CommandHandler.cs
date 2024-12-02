using Discord;
using Discord.WebSocket;
using DiscordBot.Commands.Interfaces;
using DiscordBot.Repositories.Interfaces;
using DiscordBot.Services.Interfaces;

namespace DiscordBot.Services
{
    public class CommandHandler : ICommandHandler
    {
        private IConfigurationRepository _configurationRepository;
        private IVoice _voice;

        public CommandHandler(IConfigurationRepository configurationRepository, IVoice voice)
        {
            _configurationRepository = configurationRepository ?? throw new NullReferenceException(nameof(IConfigurationRepository));
            _voice = voice ?? throw new NullReferenceException(nameof(IVoice));
        }

        public Task HandleButtonExecuted(SocketMessageComponent component)
        {
            if (component == null || component.User.IsBot)
            {
                return Task.CompletedTask;
            }

            // Run inside a task to avoid "handler is blocking the gateway task" errors
            _ = Task.Run(async () =>
            {
                // Use components id to filter which one was pressed
                switch (component.Data.CustomId)
                {
                    case "test":
                        await component.RespondAsync("Pressed id-play-button");
                        break;
                    case "id-rewind-song-button":
                        break;
                    case "id-stop-playing-button":
                        break;
                    case "id-skip-song-button":
                        break;
                }
            });

            return Task.CompletedTask;
        }

        // Slash commands are created in DiscordClientService
        public Task HandleSlashCommandAsync(SocketSlashCommand command)
        {
            if (command == null || command.CommandName == "" || command.User.IsBot)
            {
                return Task.CompletedTask;
            }

            // Run inside a task to avoid "handler is blocking the gateway task" errors
            _ = Task.Run(async () =>
            {
                switch (command.CommandName)
                {
                    case "play":
                        await _voice.Play(command);
                        break;
                    case "list-queue":
                        break;
                    case "clear-queue":
                        await _voice.ClearQueue(command);
                        break;

                }

            });

            return Task.CompletedTask;
        }

        public Task HandleMessageReceivedAsync(SocketMessage message)
        {
            // Ignore messages if prefix is missing or sender is a bot
            string prefix = _configurationRepository.GetPrefix();

            if (message == null || message.Content.IndexOf(prefix) != 0 || message.Author.IsBot)
            {
                return Task.CompletedTask;
            }

            // Run inside a task to avoid "handler is blocking the gateway task" errors
            _ = Task.Run(async () =>
            {
                // Sanitize and remove prefix because switch cases HATE dynamic strings :)
                string content = message.Content.Trim().Substring(1, message.Content.Length - prefix.Length);

                switch (content)
                {
                    case "test":
                        await message.Channel.SendMessageAsync($"Received !test command");
                        break;
                }
            });

            return Task.CompletedTask;
        }

        public Task HandleReactionAddedAsync(Cacheable<IUserMessage, ulong> cacheable1, Cacheable<IMessageChannel, ulong> cacheable2, SocketReaction reaction)
        {
            if (reaction.User.Value == null || reaction.User.Value.IsBot)
            {
                return Task.CompletedTask;
            }

            // Run inside a task to avoid "handler is blocking the gateway task" errorsd
            _ = Task.Run(async () =>
            {
                await reaction.Channel.SendMessageAsync($"{reaction.User.Value.GlobalName} reacted to a message");

            });

            return Task.CompletedTask;
        }

    }
}
