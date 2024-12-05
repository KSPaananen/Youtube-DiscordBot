namespace DiscordBot.Modules.Interfaces
{
    public interface IFFmpeg
    {
        Stream GetAudioStreamFromUrl(string url);

    }
}
