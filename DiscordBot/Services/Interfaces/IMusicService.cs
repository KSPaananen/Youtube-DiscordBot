using Discord.WebSocket;

namespace DiscordBot.Services.Interfaces
{
    public interface IMusicService
    {
        Task Play(SocketSlashCommand command);

        Task SkipSong(ulong GuildId, SocketMessageComponent component);

    }
}
