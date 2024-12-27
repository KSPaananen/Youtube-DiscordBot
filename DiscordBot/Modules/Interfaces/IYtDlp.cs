using Discord.WebSocket;
using DiscordBot.Models;

namespace DiscordBot.Modules.Interfaces
{
    public interface IYtDlp
    {
        List<SongData> GetSongFromSlashCommand(SocketSlashCommand command);

    }
}
