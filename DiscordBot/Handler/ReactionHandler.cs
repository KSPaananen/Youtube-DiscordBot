using Discord;
using Discord.WebSocket;
using DiscordBot.Handler.Interfaces;

namespace DiscordBot.Handler
{
    public class ReactionHandler : IReactionHandler
    {
        public ReactionHandler()
        {

        }

        public Task HandleReactionAdded(Cacheable<IUserMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> cachedChannel, SocketReaction reaction)
        {
            return Task.CompletedTask;
        }

        public Task HandleReactionRemovedAsync(Cacheable<IUserMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> cachedChannel, SocketReaction reaction)
        {
            return Task.CompletedTask;
        }

        public Task HandleReactionsClearedAsync(Cacheable<IUserMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> cachedChannel)
        {
            return Task.CompletedTask;
        }


    }
}
