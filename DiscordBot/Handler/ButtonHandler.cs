using Discord.WebSocket;
using DiscordBot.Handler.Interfaces;
using DiscordBot.Services.Interfaces;
using System.Reflection;

namespace DiscordBot.Handler
{
    public class ButtonHandler : IButtonHandler
    {
        private IMusicService _musicService;

        public ButtonHandler(IMusicService musicService)
        {
            _musicService = musicService ?? throw new NullReferenceException(nameof(musicService));
        }

        public Task HandleButtonExecuted(SocketMessageComponent component)
        {
            if (component == null || component.GuildId is not ulong guildId)
            {
                return Task.CompletedTask;
            }

            // Run inside a task to avoid "Handler is blocking the gateway task" errors
            _ = Task.Run(async () =>
            {
                try
                {
                    // Use components id to filter which one was pressed
                    switch (component.Data.CustomId)
                    {
                        case "embed-stop-playing-button":
                            await _musicService.StopPlayingAsync(guildId, null, component);
                            break;
                        case "embed-skip-button":
                            await _musicService.SkipSongAsync(guildId, null, component);
                            break;
                        case "embed-clear-queue-button":
                            await _musicService.ClearQueueAsync(guildId, null, component);
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


    }
}
