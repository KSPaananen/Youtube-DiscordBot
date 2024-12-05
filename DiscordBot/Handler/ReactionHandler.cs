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

        public Task HandleReactionAddedAsync(Cacheable<IUserMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> cachedChannel, SocketReaction reaction)
        {
            if (reaction.User.Value == null || reaction.User.Value.IsBot)
            {
                return Task.CompletedTask;
            }

            // Run inside a task to avoid "handler is blocking the gateway task" errors
            _ = Task.Run(async () =>
            {
                await reaction.Channel.SendMessageAsync($"{reaction.User.Value.GlobalName} reacted to a message");
            });

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
