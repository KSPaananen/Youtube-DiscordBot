using Discord;
using Discord.WebSocket;

namespace DiscordBot.Handler.Interfaces
{
    public interface IReactionHandler
    {
        Task HandleReactionAdded(Cacheable<IUserMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> cachedChannel, SocketReaction reaction);

        Task HandleReactionRemovedAsync(Cacheable<IUserMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> cachedChannel, SocketReaction reaction);

        Task HandleReactionsClearedAsync(Cacheable<IUserMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> cachedChannel);

    }
}
