using Discord.WebSocket;
using DiscordBot.Handler.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Handler
{
    public class ButtonHandler : IButtonHandler
    {
        public ButtonHandler()
        {

        }

        public Task HandleButtonExecutedAsync(SocketMessageComponent component)
        {
            if (component == null || component.User.IsBot)
            {
                return Task.CompletedTask;
            }

            // Run inside a task to avoid "Handler is blocking the gateway task" errors
            _ = Task.Run(async () =>
            {
                // Use components id to filter which one was pressed
                switch (component.Data.CustomId)
                {
                    case "test":
                        await component.RespondAsync("Pressed id-play-button");
                        break;
                    case "id-rewind-song-button":
                        break;
                    case "id-stop-playing-button":
                        break;
                    case "id-skip-song-button":
                        break;
                }
            });

            return Task.CompletedTask;
        }


    }
}
