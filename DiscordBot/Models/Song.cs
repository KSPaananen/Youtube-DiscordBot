using Discord.WebSocket;

namespace DiscordBot.Models
{
    public class Song
    {
        public string Title { get; set; } = string.Empty;

        public string VideoUrl { get; set; } = string.Empty;

        public string AudioUrl { get; set; } = string.Empty;

        public string ThumbnailUrl { get; set; } = string.Empty;

        public TimeSpan Duration { get; set; }

        public required SocketUser Requester { get; set; }

    }
}
