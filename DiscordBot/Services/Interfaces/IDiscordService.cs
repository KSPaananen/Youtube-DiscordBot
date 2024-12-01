using Discord.WebSocket;

namespace DiscordBot.Services.Interfaces
{
    public interface IDiscordService
    {
        Task MessageReceivedAsync(SocketMessage message);

    }
}
