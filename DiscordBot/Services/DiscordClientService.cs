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
        private IButtonHandler _buttonHandler;
        private IMessageHandler _messageHandler;
        private IGuildHandler _guildHandler;

        public DiscordClientService(DiscordSocketClient client, IConfigurationRepository configurationRepository, ISlashCommandHandler slashCommandHandler,
             IReactionHandler reactionHandler, IButtonHandler buttonHandler, IMessageHandler messageHandler, IGuildHandler guildHandler)
        {
            _client = client ?? throw new NullReferenceException(nameof(client));

            _configurationRepository = configurationRepository ?? throw new NullReferenceException(nameof(IConfigurationRepository));

            _slashCommandHandler = slashCommandHandler ?? throw new NullReferenceException(nameof(slashCommandHandler));
            _reactionHandler = reactionHandler ?? throw new NullReferenceException(nameof(reactionHandler));
            _buttonHandler = buttonHandler ?? throw new NullReferenceException(nameof(buttonHandler));
            _messageHandler = messageHandler ?? throw new NullReferenceException(nameof(messageHandler));
            _guildHandler = guildHandler ?? throw new NullReferenceException(nameof(guildHandler));

            // Attach methods to clients events
            _client.SlashCommandExecuted += _slashCommandHandler.HandleSlashCommand;

            _client.MessageCommandExecuted += _messageHandler.HandleMessageCommandExecuted;
            _client.MessageReceived += _messageHandler.HandleMessageReceived;
            _client.MessageDeleted += _messageHandler.HandleMessageDeleted;
            _client.MessageUpdated += _messageHandler.HandleMessageUpdated;

            _client.ReactionAdded += _reactionHandler.HandleReactionAdded;
            _client.ReactionRemoved += _reactionHandler.HandleReactionRemovedAsync;
            _client.ReactionsCleared += _reactionHandler.HandleReactionsClearedAsync;

            _client.ButtonExecuted += _buttonHandler.HandleButtonExecuted;

            _client.JoinedGuild += _guildHandler.HandleJoinedGuild;
            _client.UserJoined += _guildHandler.HandleUserJoined;
            _client.UserLeft += _guildHandler.HandleUserLeft;
            _client.UserBanned += _guildHandler.HandleUserBanned;
            _client.UserUnbanned += _guildHandler.HandleUserUnBanned;
            _client.UserVoiceStateUpdated += _guildHandler.HandleUserVoiceStateUpdated;

            _client.Disconnected += Disconnected;
            _client.Ready += ClientReady;
            _client.Log += LogAsync;
        }

        private Task Disconnected(Exception ex)
        {
            // Dispose client on 401
            Dispose();

            return Task.CompletedTask;
        }

        private Task LogAsync(LogMessage log)
        {
            if (log.Message == null && log.Exception.Message == null)
            {
                Console.WriteLine($"> [ERROR]: LogMessage.Message and LogMessage.Exception.Message were null at {this.GetType().Name} : Play()");

                return Task.CompletedTask;
            }

            // 11.12.2024 Discord.Net keeps posting unknown OpCodes to console, ignore them
            // - Unknown OpCode (11)
            // - Unknown OpCode (18)
            // - Unknown OpCode (18)

            if (log.Message != null)
            {
                if (!log.Message.Contains("OpCode"))
                {
                    Console.WriteLine($"> {log.Message}");
                }
            }
            else if (log.Exception.Message != null)
            {
                if (!log.Exception.Message.Contains("OpCode"))
                {
                    Console.WriteLine($"> [ERROR]: {log.Exception.Message}");
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

            Console.WriteLine($"> Discord client ready");

            return Task.CompletedTask;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Log in if bot token is present
            if (_configurationRepository.GetBotToken() is string token)
            {
                Console.WriteLine($"> Logging in...");

                await _client.LoginAsync(TokenType.Bot, token, true);

                await _client.StartAsync();

                var test = _client.LoginState;

                return;
            }
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
            Console.WriteLine($"> Disposing client...");

            _client.Dispose();
        }


    }
}
