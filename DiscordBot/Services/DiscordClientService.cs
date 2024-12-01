using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Repositories.Interfaces;
using DiscordBot.Services.Interfaces;
using Microsoft.Extensions.Hosting;
using System;

namespace DiscordBot.Services
{
    // Inherit IHostedService to run methods on app start
    public class DiscordClientService : IDiscordClientService, IHostedService, IDisposable
    {
        private DiscordSocketClient _client;
        private IConfigurationRepository _configurationRepository;
        private ICommandHandler _commandHandler;

        public DiscordClientService(IConfigurationRepository configurationRepository, ICommandHandler commandHandler)
        {
            _configurationRepository = configurationRepository ?? throw new NullReferenceException(nameof(IConfigurationRepository));
            _commandHandler = commandHandler ?? throw new NullReferenceException(nameof(ICommandHandler));

            // Set socket configurations
            var socketConfig = new DiscordSocketConfig
            {
                // To define multiple intents, do GatewayIntents.MessageContent | GatewayIntents.Guilds | etc...
                GatewayIntents = GatewayIntents.All
            };

            // Create a new client
            _client = new DiscordSocketClient(socketConfig);

            // Tie methods to sockets
            _client.SlashCommandExecuted += _commandHandler.HandleSlashCommandAsync;
            _client.MessageReceived += _commandHandler.HandleMessageReceivedAsync;
            _client.ReactionAdded += _commandHandler.HandleReactionAddedAsync;
            _client.ButtonExecuted += _commandHandler.HandleButtonExecuted;

            _client.Log += LogAsync;
            _client.Ready += ClientReady;

        }

        public Task LogAsync(LogMessage message)
        {
            Console.WriteLine($"> {message.Message}");

            return Task.CompletedTask;
        }
        public async Task ClientReady()
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

            // Create different commands to be used
            await CreateSlashCommands();

            return;
        }

        public async Task CreateSlashCommands()
        {
            // Add all commands to this list
            List<ApplicationCommandProperties> globalAppCommandsList = new();

            // Play music command
            SlashCommandBuilder globalPlayCommand = new ();

            globalPlayCommand.WithName("play");
            globalPlayCommand.WithDescription("Play music in a voicechat. For example: /play ");
            globalPlayCommand.AddOption("link", ApplicationCommandOptionType.String, "The user who requested resource.");
            globalAppCommandsList.Add(globalPlayCommand.Build()); // Add to global commands list

            // Write all global command from list
            await _client.BulkOverwriteGlobalApplicationCommandsAsync(globalAppCommandsList.ToArray());

            return;
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
