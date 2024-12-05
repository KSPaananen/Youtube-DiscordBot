using Discord.WebSocket;

namespace DiscordBot.Handler.Interfaces
{
    public interface ISlashCommandHandler
    {
        Task HandleSlashCommandAsync(SocketSlashCommand command);

        Task CreateSlashCommandsAsync(DiscordSocketClient client);

    }
}
