using Discord;
using Discord.WebSocket;
using DiscordBot.Handler.Interfaces;
using DiscordBot.Services.Interfaces;

namespace DiscordBot.Handler
{
    public class SlashCommandHandler : ISlashCommandHandler
    {
        private IMusicService _musicService;

        private SocketSlashCommand? _command;

        public SlashCommandHandler(IMusicService musicService)
        {
            _musicService = musicService ?? throw new NullReferenceException(nameof(musicService));
        }

        public Task HandleSlashCommandAsync(SocketSlashCommand command)
        {
            if (command == null || command.CommandName == "" || command.User.IsBot)
            {
                return Task.CompletedTask;
            }

            _command = command;

            // Run inside a task to avoid "handler is blocking the gateway task" errors
            _ = Task.Run(async () =>
            {
                switch (command.CommandName)
                {
                    case "play":
                        await _musicService.Play(command);

                        break;
                }

            });

            return Task.CompletedTask;
        }

        public async Task CreateSlashCommandsAsync(DiscordSocketClient client)
        {
            // Add all commands to this list
            List<ApplicationCommandProperties> globalAppCommandsList = new();

            // Play music command
            var globalPlayCommand = new SlashCommandBuilder();

            globalPlayCommand.WithName("play");
            globalPlayCommand.WithDescription("Play music in a voicechat");
            globalPlayCommand.AddOption("query", ApplicationCommandOptionType.String, "Search music with a query or provide a link to the song", true);
            globalAppCommandsList.Add(globalPlayCommand.Build());

            // List music queue command
            var globalListQueueCommand = new SlashCommandBuilder();

            globalListQueueCommand.WithName("list-queue");
            globalListQueueCommand.WithDescription("List all songs in the queue");
            globalAppCommandsList.Add(globalListQueueCommand.Build());

            // clear music queue command
            var globalClearQueueCommand = new SlashCommandBuilder();

            globalClearQueueCommand.WithName("clear-queue");
            globalClearQueueCommand.WithDescription("Clear music queue");
            globalAppCommandsList.Add(globalClearQueueCommand.Build());

            var requestOptions = new RequestOptions()
            {
                RetryMode = RetryMode.Retry502,
            };

            // Write all global command from list
            await client.BulkOverwriteGlobalApplicationCommandsAsync(globalAppCommandsList.ToArray(), requestOptions);

            return;
        }


    }
}
