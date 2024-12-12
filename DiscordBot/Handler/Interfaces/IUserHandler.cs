using Discord.WebSocket;

namespace DiscordBot.Handler.Interfaces
{
    public interface IUserHandler
    {
        Task HandleUserJoined(SocketGuildUser user);

        Task HandleUserLeft(SocketGuild server, SocketUser user);

        Task HandleUserBanned(SocketUser user, SocketGuild server);

        Task HandleUserUnBanned(SocketUser user, SocketGuild server);

    }
}
