using Discord;
using Discord.WebSocket;
using DiscordBot.Handler.Interfaces;
using DiscordBot.Repositories.Interfaces;
using DiscordBot.Services.Interfaces;
using Microsoft.Extensions.Hosting;

namespace DiscordBot.Services
{
    // Inherit IHostedService to run methods on app start
    public class DiscordClientService : IDiscordClientService, IHostedService, IDisposable
    {
        private IConfigurationRepository _configurationRepository;

        private ISlashCommandHandler _slashCommandHandler;
        private IReactionHandler _reactionHandler;
        private IUserHandler _userHandler;
        private IButtonHandler _buttonHandler;
        private IMessageHandler _messageHandler;

        private DiscordSocketClient _client;

        public DiscordClientService(IConfigurationRepository configurationRepository, ISlashCommandHandler slashCommandHandler, IReactionHandler reactionHandler,
            IUserHandler userHandler, IButtonHandler buttonHandler, IMessageHandler messageHandler)
        {
            _configurationRepository = configurationRepository ?? throw new NullReferenceException(nameof(IConfigurationRepository));

            _slashCommandHandler = slashCommandHandler ?? throw new NullReferenceException(nameof(slashCommandHandler));
            _reactionHandler = reactionHandler ?? throw new NullReferenceException(nameof(reactionHandler));
            _userHandler = userHandler ?? throw new NullReferenceException(nameof(userHandler));
            _buttonHandler = buttonHandler ?? throw new NullReferenceException(nameof(buttonHandler));
            _messageHandler = messageHandler ?? throw new NullReferenceException(nameof(messageHandler));

            var socketConfig = new DiscordSocketConfig
            {
                // Syntax for multiple GatewayIntents GatewayIntents.Guilds | GatewayIntents.GuildBans | ...
                // These are all unprivileged intents except:
                // - GuildScheduledEvents
                // - GuildInvites
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildBans | GatewayIntents.GuildEmojis | GatewayIntents.GuildIntegrations |
                    GatewayIntents.GuildWebhooks | GatewayIntents.GuildVoiceStates | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions |
                    GatewayIntents.GuildMessageTyping | GatewayIntents.DirectMessages | GatewayIntents.DirectMessageReactions | GatewayIntents.DirectMessageTyping |
                    GatewayIntents.AutoModerationConfiguration | GatewayIntents.AutoModerationActionExecution | GatewayIntents.GuildMessagePolls |
                    GatewayIntents.DirectMessagePolls,
                MessageCacheSize = 10,
                AlwaysDownloadDefaultStickers = true,
                AlwaysResolveStickers = true,
                AlwaysDownloadUsers = true,
                AuditLogCacheSize = 10,
                LogLevel = LogSeverity.Verbose

            };

            // Create a new client
            _client = new DiscordSocketClient(socketConfig);

            _client.SlashCommandExecuted += _slashCommandHandler.HandleSlashCommandAsync;

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

            _client.Ready += ClientReady;
            _client.Log += LogAsync;
        }

        private Task LogAsync(LogMessage message)
        {
            Console.WriteLine($"> {message.Message}");

            return Task.CompletedTask;
        }

        public DiscordSocketClient GetClient()
        {
            return _client;
        }

        private Task ClientReady()
        {
            // Display basic information about the bot in console
            switch (_client.LoginState)
            {
                case LoginState.LoggedIn:
                    Console.WriteLine($"> Bot information: \n  " +
                        $"- Globalname: {_client.CurrentUser.GlobalName} \n  " +
                        $"- Username: {_client.CurrentUser.Username} \n  " +
                        $"- Current status: {_client.CurrentUser.Status}");
                    break;
                case LoginState.LoggedOut:
                    Console.WriteLine($"> Error logging in. Check that the tokens and keys are correct");
                    break;
            }

            // Create new slash commands on app start
            _slashCommandHandler.CreateSlashCommandsAsync(_client);

            return Task.CompletedTask;
        }


        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine($"> Starting application...");

            // Login
            string botToken = _configurationRepository.GetBotToken();

            await _client.LoginAsync(TokenType.Bot, botToken, true);
            await _client.StartAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
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
