using Discord;
using Discord.WebSocket;

namespace DiscordBot.Handler.Interfaces
{
    public interface IMessageHandler
    {
        Task HandleMessageCommandExecuted(SocketMessageCommand command);

        Task HandleMessageReceived(SocketMessage message);

        Task HandleMessageDeleted(Cacheable<IMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> cachedChannel);

        Task HandleMessageUpdated(Cacheable<IMessage, ulong> cachedMessage, SocketMessage message, ISocketMessageChannel channel);

    }
}
