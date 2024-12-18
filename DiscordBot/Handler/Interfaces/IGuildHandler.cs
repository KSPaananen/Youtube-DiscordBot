using Discord.WebSocket;

namespace DiscordBot.Handler.Interfaces
{
    public interface IGuildHandler
    {
        Task HandleJoinGuild(SocketGuild guild);

        Task HandleUserJoined(SocketGuildUser user);

        Task HandleUserLeft(SocketGuild server, SocketUser user);

        Task HandleUserBanned(SocketUser user, SocketGuild server);

        Task HandleUserUnBanned(SocketUser user, SocketGuild server);

        Task UserVoiceStateUpdated(SocketUser user, SocketVoiceState joinedState, SocketVoiceState leftState);

    }
}
