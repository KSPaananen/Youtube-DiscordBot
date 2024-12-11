using Discord;
using Discord.WebSocket;
using DiscordBot.Handler.Interfaces;
using DiscordBot.Repositories.Interfaces;
using DiscordBot.Services.Interfaces;
using Microsoft.Extensions.Hosting;

namespace DiscordBot.Services
{
    public class DiscordClientService : IDiscordClientService, IHostedService, IDisposable
    {
        private DiscordSocketClient _client;

        private IConfigurationRepository _configurationRepository;

        private ISlashCommandHandler _slashCommandHandler;
        private IReactionHandler _reactionHandler;
        private IUserHandler _userHandler;
        private IButtonHandler _buttonHandler;
        private IMessageHandler _messageHandler;
        private IGuildHandler _guildHandler;

        public DiscordClientService(DiscordSocketClient client, IConfigurationRepository configurationRepository, ISlashCommandHandler slashCommandHandler,
             IReactionHandler reactionHandler, IUserHandler userHandler, IButtonHandler buttonHandler, IMessageHandler messageHandler, IGuildHandler guildHandler)
        {
            _client = client ?? throw new NullReferenceException(nameof(client));

            _configurationRepository = configurationRepository ?? throw new NullReferenceException(nameof(IConfigurationRepository));

            _slashCommandHandler = slashCommandHandler ?? throw new NullReferenceException(nameof(slashCommandHandler));
            _reactionHandler = reactionHandler ?? throw new NullReferenceException(nameof(reactionHandler));
            _userHandler = userHandler ?? throw new NullReferenceException(nameof(userHandler));
            _buttonHandler = buttonHandler ?? throw new NullReferenceException(nameof(buttonHandler));
            _messageHandler = messageHandler ?? throw new NullReferenceException(nameof(messageHandler));
            _guildHandler = guildHandler ?? throw new NullReferenceException(nameof(guildHandler));

            // Attach methods to clients events
            _client.SlashCommandExecuted += _slashCommandHandler.HandleSlashCommand;

            _client.ReactionAdded += _reactionHandler.HandleReactionAddedAsync;
            _client.ReactionRemoved += _reactionHandler.HandleReactionRemovedAsync;
            _client.ReactionsCleared += _reactionHandler.HandleReactionsClearedAsync;

            _client.UserJoined += _userHandler.HandleUserJoinedAsync;
            _client.UserLeft += _userHandler.HandleUserLeftAsync;
            _client.UserBanned += _userHandler.HandleUserBannedAsync;
            _client.UserUnbanned += _userHandler.HandleUserUnBannedAsync;

            _client.ButtonExecuted += _buttonHandler.HandleButtonExecutedAsync;

            _client.MessageCommandExecuted += _messageHandler.HandleMessageCommandExecutedAsync;
            _client.MessageReceived += _messageHandler.HandleMessageReceivedAsync;
            _client.MessageDeleted += _messageHandler.HandleMessageDeletedAsync;
            _client.MessageUpdated += _messageHandler.HandleMessageUpdatedAsync;

            _client.JoinedGuild += _guildHandler.HandleJoinGuildAsync;

            _client.Ready += ClientReady;
            _client.Log += LogAsync;
        }

        private Task LogAsync(LogMessage log)
        {
            // 11.12.2024 Discord.Net keeps posting unknown OpCodes to console, ignore them
            // - Unknown OpCode (11)
            // - Unknown OpCode (18)
            // - Unknown OpCode (18)

            if (!log.Message.Contains("OpCode") && log.Exception.Message.Contains("OpCode"))
            {
                if (log.Exception != null)
                {
                    Console.WriteLine($"> [ERROR]: {log.Exception.Message}");
                }
                else
                {
                    Console.WriteLine($"> {log.Message}");
                }
            }

            return Task.CompletedTask;
        }

        private Task ClientReady()
        {
            // Display basic information about the bot in console
            switch (_client.LoginState)
            {
                case LoginState.LoggedIn:
                    Console.WriteLine($"> Bot information: \n  " +
                        $"- ID: {_client.CurrentUser.Id} \n  " +
                        $"- Display name: {_client.CurrentUser.Username} \n  " +
                        $"- Username: {_client.CurrentUser.Username}#{_client.CurrentUser.Discriminator} \n  " +
                        $"- Current status: {_client.CurrentUser.Status} \n  " +
                        $"- Verified: {_client.CurrentUser.IsVerified} \n  " +
                        $"- Mfa: {_client.CurrentUser.IsMfaEnabled} \n  " +
                        $"- Clients: {_client.CurrentUser.ActiveClients.Count} \n  " +
                        $"- Guilds: {_client.Guilds.Count} \n  " +
                        $"- Created: {(_client.CurrentUser.CreatedAt.UtcDateTime).ToString().Substring(0, (_client.CurrentUser.CreatedAt.UtcDateTime).ToString().IndexOf(" "))} ");

                    break;
                case LoginState.LoggedOut:
                    Console.WriteLine($"> Error logging in. Check that the tokens and keys are correct");

                    break;
            }

            Console.WriteLine($"> Creating slash commands...");

            // Create new slash commands on app start
            _slashCommandHandler.CreateSlashCommandsAsync();

            Console.WriteLine($"> Application ready \n");

            return Task.CompletedTask;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine($"> Starting application...");

            // Login
            string botToken = _configurationRepository.GetBotToken();

            Console.WriteLine($"> Logging in...");

            await _client.LoginAsync(TokenType.Bot, botToken, true);
            await _client.StartAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine($"> Logging out");

            // Logout client
            await _client.LogoutAsync();

            // Stop client
            await _client.StopAsync();

            // Inform user on console
            Console.WriteLine($"> {_client.CurrentUser.Username} stopped");
        }

        public void Dispose()
        {
            _client.Dispose();
        }


    }
}
