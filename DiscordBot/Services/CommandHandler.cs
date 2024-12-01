using Discord;
using Discord.WebSocket;
using DiscordBot.Repositories.Interfaces;
using DiscordBot.Services.Interfaces;
using Microsoft.Extensions.Configuration;

namespace DiscordBot.Services
{
    public class CommandHandler : ICommandHandler
    {
        private IConfigurationRepository _configurationRepository;

        public CommandHandler(IConfigurationRepository configurationRepository)
        {
            _configurationRepository = configurationRepository ?? throw new NullReferenceException(nameof(IConfigurationRepository));
        }

        public async Task HandleButtonExecuted(SocketMessageComponent component)
        {
            if (component == null || component.User.IsBot)
            {
                return;
            }

            // Use components id to filter which one was pressed
            switch (component.Data.CustomId)
            {
                case "id-play-button":
                    await component.RespondAsync("Pressed id-play-button");
                    break;
            }
        }

        public async Task HandleMessageReceivedAsync(SocketMessage message)
        {
            // Ignore messages if prefix is missing or sender is a bot
            string prefix = _configurationRepository.GetPrefix();

            if (message == null || message.Content.IndexOf(prefix) != 0 || message.Author.IsBot)
            {
                return;
            }

            // Sanitize and remove prefix because switch cases HATE dynamic strings :)
            string content = message.Content.Trim().Substring(1, message.Content.Length - prefix.Length);

            switch (content)
            {
                case "aa":
                    await message.Channel.SendMessageAsync($"Received message");
                    break;
            }

            return;
        }

        public Task HandleReactionAddedAsync(Cacheable<IUserMessage, ulong> cacheable1, Cacheable<IMessageChannel, ulong> cacheable2, SocketReaction reaction)
        {
            throw new NotImplementedException();
        }

        // Slash commands are created in DiscordClientService
        public async Task HandleSlashCommandAsync(SocketSlashCommand command)
        {
            if (command == null || command.CommandName == "" || command.User.IsBot)
            {
                return;
            }

            ComponentBuilder builder = new ComponentBuilder().WithButton("Play", "id-play-button");

            switch (command.CommandName)
            {
                case "play":
                    await command.RespondAsync("Received play slashcommand", components: builder.Build());
                    break;
            }

            
        }


    }
}
