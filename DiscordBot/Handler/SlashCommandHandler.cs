using Discord;
using Discord.WebSocket;
using DiscordBot.Handler.Interfaces;
using DiscordBot.Repositories.Interfaces;
using DiscordBot.Services.Interfaces;
using System.Reflection;

namespace DiscordBot.Handler
{
    public class SlashCommandHandler : ISlashCommandHandler
    {
        private IConfigurationRepository _configurationRepository;

        private DiscordSocketClient _client;

        private IMusicService _musicService;

        public SlashCommandHandler(IConfigurationRepository configurationRepository, DiscordSocketClient client, IMusicService musicService)
        {
            _configurationRepository = configurationRepository ?? throw new NullReferenceException(nameof(configurationRepository));

            _client = client ?? throw new NullReferenceException(nameof(client));

            _musicService = musicService ?? throw new NullReferenceException(nameof(musicService));
        }

        public Task HandleSlashCommand(SocketSlashCommand command)
        {
            if (command == null || command.CommandName == "" || command.GuildId is not ulong guildId)
            {
                return Task.CompletedTask;
            }

            // Run inside a task to avoid "handler is blocking the gateway task" errors
            _ = Task.Run(async () =>
            {
                try
                {
                    switch (command.CommandName)
                    {
                        case "play":
                            await _musicService.PlayAsync( command);
                            break;
                        case "stop":
                            await _musicService.StopPlayingAsync(guildId, command);
                            break;
                        case "skip":
                            await _musicService.SkipSongAsync(guildId, command);
                            break;
                        case "clear-queue":
                            await _musicService.ClearQueueAsync(guildId, command);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message != null ? $"[ERROR]: {ex.Message}" : $"[ERROR]: Something went wrong in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                }
            });

            return Task.CompletedTask;
        }

        public async Task CreateSlashCommandsAsync()
        {
            // Add all commands to this list
            List<ApplicationCommandProperties> globalAppCommandsList = new();

            // Play music command
            var globalPlayCommand = new SlashCommandBuilder();
            globalPlayCommand.WithName("play");
            globalPlayCommand.WithDescription("Play music in a voice chat. Supports queries, links & playlists");
            globalPlayCommand.AddOption("query", ApplicationCommandOptionType.String, "Search for music with a query or provide a link to a song or playlist", true);
            globalAppCommandsList.Add(globalPlayCommand.Build());

            var globalStopPlayingCommand = new SlashCommandBuilder();
            globalStopPlayingCommand.WithName("stop");
            globalStopPlayingCommand.WithDescription("Stop playing and disconnect the bot from the voice channel");
            globalAppCommandsList.Add(globalStopPlayingCommand.Build());

            var globalSkipCommand = new SlashCommandBuilder();
            globalSkipCommand.WithName("skip");
            globalSkipCommand.WithDescription("Skip the song thats currently playing");
            globalAppCommandsList.Add(globalSkipCommand.Build());

            var globalClearQueueCommand = new SlashCommandBuilder();
            globalClearQueueCommand.WithName("clear-queue");
            globalClearQueueCommand.WithDescription("Clear the song queue");
            globalAppCommandsList.Add(globalClearQueueCommand.Build());

            var requestOptions = new RequestOptions()
            {
                RetryMode = RetryMode.Retry502,
            };

            // Write all global command from list
            await _client.BulkOverwriteGlobalApplicationCommandsAsync(globalAppCommandsList.ToArray(), requestOptions);

            return;
        }


    }
}
