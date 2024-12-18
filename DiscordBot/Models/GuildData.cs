using Discord;
using Discord.Audio;

namespace DiscordBot.Models
{
    public class GuildData
    {
        public IUserMessage? UserMessage { get; set; }

        public IAudioClient? AudioClient { get; set; }

        public CancellationTokenSource cTokenSource { get; set; } = new CancellationTokenSource();

        public List<SongData> Queue { get; set; } = new List<SongData>();

        public bool FirstSong { get; set; } = true;

    }
}
