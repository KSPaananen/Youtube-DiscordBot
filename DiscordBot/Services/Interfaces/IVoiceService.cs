using Discord.WebSocket;

namespace DiscordBot.Services.Interfaces
{
    public interface IVoiceService
    {
        Task Play(SocketSlashCommand command);

        Task ClearQueue(SocketSlashCommand command);

        Task ListQueue(SocketSlashCommand command);
    }
}
