using Discord;
using Discord.Audio;

namespace DiscordBot.Models
{
    public class GuildData
    {
        private readonly object _lock = new object();

        private IUserMessage? _nowPlayingMessage;
        public IUserMessage? NowPlayingMessage
        {
            get
            {
                lock (_lock)
                {
                    return _nowPlayingMessage;
                }
            }
            set
            {
                lock (_lock)
                {
                    _nowPlayingMessage = value;
                }
            }
        }

        private IAudioClient? _audioClient;
        public IAudioClient? AudioClient
        {
            get
            {
                lock (_lock)
                {
                    return _audioClient;
                }
            }
            set
            {
                lock (_lock)
                {
                    _audioClient = value;
                }
            }
        }

        private CancellationTokenSource _cTokenSource = new CancellationTokenSource();
        public CancellationTokenSource cTokenSource
        {
            get
            {
                lock (_lock)
                {
                    return _cTokenSource;
                }
            }
            set
            {
                lock (_lock)
                {
                    _cTokenSource = value;
                }
            }
        }

        private List<SongData> _queue = new List<SongData>();
        public List<SongData> Queue
        {
            get
            {
                lock (_lock)
                {
                    return _queue;
                }
            }
            set
            {
                lock (_lock)
                {
                    _queue = value;
                }
            }
        }

        private bool _currentlyPlaying = false;
        public bool CurrentlyPlaying
        {
            get
            {
                lock (_lock)
                {
                    return _currentlyPlaying;
                }
            }
            set
            {
                lock (_lock)
                {
                    _currentlyPlaying = value;
                }
            }
        }

        private bool _firstsong = true;
        public bool FirstSong
        {
            get
            {
                lock (_lock)
                {
                    return _firstsong;
                }
            }
            set
            {
                lock (_lock)
                {
                    _firstsong = value;
                }
            }
        }


    }
}
