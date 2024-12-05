using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules.Interfaces
{
    public interface IFFmpeg
    {
        Stream GetAudioStreamFromUrl(string url);

    }
}
