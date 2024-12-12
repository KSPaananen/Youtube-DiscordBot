using Discord.WebSocket;
using DiscordBot.Handler.Interfaces;

namespace DiscordBot.Handler
{
    public class UserHandler : IUserHandler
    {
        public UserHandler()
        {

        }

        public Task HandleUserBanned(SocketUser user, SocketGuild server)
        {
            return Task.CompletedTask;
        }

        public Task HandleUserJoined(SocketGuildUser user)
        {
            return Task.CompletedTask;
        }

        public Task HandleUserLeft(SocketGuild server, SocketUser user)
        {
            return Task.CompletedTask;
        }

        public Task HandleUserUnBanned(SocketUser user, SocketGuild server)
        {
            return Task.CompletedTask;
        }


    }
}
