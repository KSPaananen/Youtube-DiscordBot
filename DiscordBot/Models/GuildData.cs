using Discord;
using Discord.Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Models
{
    public class GuildData
    {
        public IUserMessage? UserMessage { get; set; }

        public IAudioClient? AudioClient { get; set; }

        public CancellationTokenSource cTokenSource { get; set; } = new CancellationTokenSource();

        public List<SongData> Queue { get; set; } = new List<SongData>();

        public bool FirstSong { get; set; }

        public ulong StreamID
        {
            get
            {
                return AudioClient != null && AudioClient.GetStreams().Any() ? AudioClient.GetStreams().First().Key : 0;
            }
            set
            {

            }
        }


    }
}
