using Discord.WebSocket;

namespace DiscordBot.Services.Interfaces
{
    public interface IMusicService
    {
        Task Play(SocketSlashCommand command);

        Task SkipSong(ulong guildId, SocketSlashCommand? command = null, SocketMessageComponent? component = null);

        Task ClearQueue(ulong guildId, SocketSlashCommand? command = null, SocketMessageComponent? component = null);

    }
}
