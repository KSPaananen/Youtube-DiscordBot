using Discord.WebSocket;
using DiscordBot.Models;

namespace DiscordBot.Modules.Interfaces
{
    public interface IYtDlp
    {
        SongData GetSongFromSlashCommand(SocketSlashCommand command);

    }
}
