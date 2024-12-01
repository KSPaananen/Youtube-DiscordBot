using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Repositories.Interfaces;
using DiscordBot.Services.Interfaces;
using Microsoft.Extensions.Hosting;

namespace DiscordBot.Services
{
    // Inherit IHostedService to run methods on app start
    public class DiscordService : IDiscordService, IHostedService, IDisposable
    {
        private DiscordSocketClient _client;
        private IConfigurationRepository _configurationRepository;

        private string _name;

        public DiscordService(IConfigurationRepository configurationRepository)
        {
            _configurationRepository = configurationRepository ?? throw new NullReferenceException(nameof(IConfigurationRepository));

            // Read bot name from configuration
            _name = _configurationRepository.GetName();

            // Create a new client
            _client = new DiscordSocketClient();

            // Tie methods to sockets
            _client.MessageReceived += MessageReceivedAsync;
            _client.Log += LogAsync;
        }

        public async Task MessageReceivedAsync(SocketMessage message)
        {
            if (message.Author.IsBot)
            {
                return;
            }
            
            switch (message.Content)
            {
                case "":
                    await message.Channel.SendMessageAsync("Why is this shit getting empty messages again???");
                    break;
            }

            return;
        }

        public Task LogAsync(LogMessage message)
        {
            switch (message.Exception)
            {
                case CommandException exception:
                    Console.WriteLine($"[Failed to execute command {exception.Command.Aliases.First()}] \n in channel {exception.Context.Channel}");
                    break;
            }


            return Task.CompletedTask;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine($"> Starting {_name}...");

            // Login
            string botToken = _configurationRepository.GetBotToken();

            await _client.LoginAsync(TokenType.Bot, botToken, true);
            await _client.StartAsync();

            // Display loginstate in console
            switch (_client.LoginState)
            {
                case LoginState.LoggedIn:
                    Console.WriteLine($"> {_name} succesfully logged in");
                    break;
                case LoginState.LoggedOut:
                    Console.WriteLine($"> {_name} failed to login");
                    break;
            }

        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            // Logout client
            await _client.LogoutAsync();

            // Stop client
            await _client.StopAsync();

            // Inform user on console
            Console.WriteLine($"{_name} stopped");
        }

        public void Dispose()
        {
            _client.Dispose();
        }


    }
}
