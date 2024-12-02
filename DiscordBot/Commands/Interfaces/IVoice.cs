using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Commands.Interfaces
{
    public interface IVoice
    {
        Task Play(SocketSlashCommand command);

        Task ClearQueue(SocketSlashCommand command);
    }
}
