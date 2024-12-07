using DiscordBot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules.Interfaces
{
    public interface IYtDlp
    {
        Song GetSongFromQuery(string link);

    }
}
