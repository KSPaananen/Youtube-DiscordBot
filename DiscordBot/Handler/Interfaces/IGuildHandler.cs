using Discord.WebSocket;

namespace DiscordBot.Handler.Interfaces
{
    public interface IGuildHandler
    {
        Task HandleJoinGuildAsync(SocketGuild guild);

    }
}
