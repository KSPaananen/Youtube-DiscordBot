using Discord.WebSocket;

namespace DiscordBot.Services.Interfaces
{
    public interface IMusicService
    {
        Task Play(SocketSlashCommand command);

        Task SkipSong(ulong GuildId, SocketSlashCommand? command = null, SocketMessageComponent? component = null);

        Task ClearQueue(ulong GuildId, SocketSlashCommand? command = null, SocketMessageComponent? component = null);

    }
}
