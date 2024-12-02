using Discord.WebSocket;

namespace DiscordBot.Services.Interfaces
{
    public interface IDiscordClientService
    {
        Task CreateSlashCommands();

    }
}
