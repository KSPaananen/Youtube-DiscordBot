using Discord.WebSocket;

namespace DiscordBot.Services.Interfaces
{
    public interface IGuildService
    {
        Task SendJoinedGuildMessage(SocketGuild guild);

    }
}
