using Discord.WebSocket;

namespace DiscordBot.Handler.Interfaces
{
    public interface IGuildHandler
    {
        Task HandleJoinedGuild(SocketGuild guild);

        Task HandleUserJoined(SocketGuildUser user);

        Task HandleUserLeft(SocketGuild server, SocketUser user);

        Task HandleUserBanned(SocketUser user, SocketGuild server);

        Task HandleUserUnBanned(SocketUser user, SocketGuild server);

        Task HandleUserVoiceStateUpdated(SocketUser user, SocketVoiceState joinedState, SocketVoiceState leftState);

    }
}
