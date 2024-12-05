using Discord.WebSocket;

namespace DiscordBot.Handler.Interfaces
{
    public interface IUserHandler
    {
        Task HandleUserJoinedAsync(SocketGuildUser user);

        Task HandleUserLeftAsync(SocketGuild server, SocketUser user);

        Task HandleUserBannedAsync(SocketUser user, SocketGuild server);

        Task HandleUserUnBannedAsync(SocketUser user, SocketGuild server);

    }
}
