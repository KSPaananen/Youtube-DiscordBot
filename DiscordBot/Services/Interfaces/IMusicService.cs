using Discord.WebSocket;

namespace DiscordBot.Services.Interfaces
{
    public interface IMusicService
    {
        Task PlayAsync(SocketSlashCommand command);

        Task SkipSongAsync(ulong guildId, SocketSlashCommand? command = null, SocketMessageComponent? component = null);

        Task ClearQueueAsync(ulong guildId, SocketSlashCommand? command = null, SocketMessageComponent? component = null);

        Task CheckChannelStateAsync(SocketVoiceChannel channel);

        Task DisposeGuildResourcesAsync(ulong guildId);

    }
}
