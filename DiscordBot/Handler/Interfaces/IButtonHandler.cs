using Discord.WebSocket;

namespace DiscordBot.Handler.Interfaces
{
    public interface IButtonHandler
    {
        Task HandleButtonExecutedAsync(SocketMessageComponent component);

    }
}
