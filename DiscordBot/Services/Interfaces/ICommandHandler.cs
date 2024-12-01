using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services.Interfaces
{
    public interface ICommandHandler
    {
        Task HandleSlashCommandAsync(SocketSlashCommand command);

        Task HandleMessageReceivedAsync(SocketMessage message);

        Task HandleReactionAddedAsync(Cacheable<IUserMessage, ulong> cacheable1, Cacheable<IMessageChannel, ulong> cacheable2, SocketReaction reaction);

        Task HandleButtonExecuted(SocketMessageComponent component);
    }
}
