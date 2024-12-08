using DiscordBot.Models;

namespace DiscordBot.Modules.Interfaces
{
    public interface IYtDlp
    {
        Song GetSongFromQuery(string link);

    }
}
