using Discord.WebSocket;
using DiscordBot.Models;

namespace DiscordBot.Modules.Interfaces
{
    public interface IYtDlp
    {
        Song GetSongFromSlashCommand(SocketSlashCommand command);

    }
}
