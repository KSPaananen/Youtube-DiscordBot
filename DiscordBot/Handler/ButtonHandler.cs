using Discord.WebSocket;
using DiscordBot.Handler.Interfaces;
using DiscordBot.Services.Interfaces;

namespace DiscordBot.Handler
{
    public class ButtonHandler : IButtonHandler
    {
        private IMusicService _musicService;

        public ButtonHandler(IMusicService musicService)
        {
            _musicService = musicService ?? throw new NullReferenceException(nameof(musicService));
        }

        public Task HandleButtonExecutedAsync(SocketMessageComponent component)
        {
            if (component == null || component.GuildId is not ulong guildId)
            {
                return Task.CompletedTask;
            }

            // Run inside a task to avoid "Handler is blocking the gateway task" errors
            _ = Task.Run(async () =>
            {
                // Use components id to filter which one was pressed
                switch (component.Data.CustomId)
                {
                    case "skip-song-button":
                        await _musicService.SkipSong(guildId, component);
                        break;
                   
                }
            });

            return Task.CompletedTask;
        }


    }
}
