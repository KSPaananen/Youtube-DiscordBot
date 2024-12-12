using Discord;
using Discord.WebSocket;
using DiscordBot.Handler.Interfaces;

namespace DiscordBot.Handler
{
    public class MessageHandler : IMessageHandler
    {
        public MessageHandler()
        {

        }

        public Task HandleMessageCommandExecuted(SocketMessageCommand command)
        {
            return Task.CompletedTask;
        }

        public Task HandleMessageDeleted(Cacheable<IMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> cachedChannel)
        {
            return Task.CompletedTask;
        }

        public Task HandleMessageReceived(SocketMessage message)
        {
            return Task.CompletedTask;
        }

        public Task HandleMessageUpdated(Cacheable<IMessage, ulong> cachedMessage, SocketMessage message, ISocketMessageChannel channel)
        {
            return Task.CompletedTask;
        }


    }
}
