using Discord.WebSocket;

namespace DiscordBot.Handler.Interfaces
{
    public interface IGuildHandler
    {
        Task HandleJoinGuild(SocketGuild guild);

    }
}
