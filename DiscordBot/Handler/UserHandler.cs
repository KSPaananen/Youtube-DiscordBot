using Discord.WebSocket;
using DiscordBot.Handler.Interfaces;

namespace DiscordBot.Handler
{
    public class UserHandler : IUserHandler
    {
        public UserHandler()
        {

        }

        public Task HandleUserBannedAsync(SocketUser user, SocketGuild server)
        {
            return Task.CompletedTask;
        }

        public Task HandleUserJoinedAsync(SocketGuildUser user)
        {
            return Task.CompletedTask;
        }

        public Task HandleUserLeftAsync(SocketGuild server, SocketUser user)
        {
            return Task.CompletedTask;
        }

        public Task HandleUserUnBannedAsync(SocketUser user, SocketGuild server)
        {
            return Task.CompletedTask;
        }
    }
}
