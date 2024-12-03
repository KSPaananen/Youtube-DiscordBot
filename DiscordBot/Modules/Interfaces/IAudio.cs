using System.Diagnostics;

namespace DiscordBot.Modules.Interfaces
{
    public interface IAudio
    {
        string GetAudioUrlFromLink(string link);

        Process GetAudioStreamFromUrl(string url);

    }
}
