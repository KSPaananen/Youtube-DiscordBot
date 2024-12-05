using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services.Interfaces
{
    public interface IMusicService
    {
        Task Play(SocketSlashCommand command);
    }
}
