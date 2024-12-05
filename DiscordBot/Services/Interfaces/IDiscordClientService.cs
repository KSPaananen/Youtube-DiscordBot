using Discord.WebSocket;

namespace DiscordBot.Services.Interfaces
{
    public interface IDiscordClientService
    {
        DiscordSocketClient GetClient();

    }
}
