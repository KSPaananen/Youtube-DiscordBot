using Discord.WebSocket;
using DiscordBot.Models;

namespace DiscordBot.Services.Interfaces
{
    public interface IMessageService
    {
        Task SendJoinedGuildMessage(SocketGuild guild);

    }
}
