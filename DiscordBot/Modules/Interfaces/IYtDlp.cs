using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules.Interfaces
{
    public interface IYtDlp
    {
        string GetAudioUrlFromLink(string link);

    }
}
