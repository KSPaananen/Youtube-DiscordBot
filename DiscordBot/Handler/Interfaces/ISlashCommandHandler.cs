using Discord.WebSocket;

namespace DiscordBot.Handler.Interfaces
{
    public interface ISlashCommandHandler
    {
        Task HandleSlashCommand(SocketSlashCommand command);

        Task CreateSlashCommandsAsync(DiscordSocketClient client);

    }
}
