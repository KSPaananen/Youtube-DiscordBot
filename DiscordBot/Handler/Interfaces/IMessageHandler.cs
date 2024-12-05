using Discord;
using Discord.WebSocket;

namespace DiscordBot.Handler.Interfaces
{
    public interface IMessageHandler
    {
        Task HandleMessageCommandExecutedAsync(SocketMessageCommand command);

        Task HandleMessageReceivedAsync(SocketMessage message);

        Task HandleMessageDeletedAsync(Cacheable<IMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> cachedChannel);

        Task HandleMessageUpdatedAsync(Cacheable<IMessage, ulong> cachedMessage, SocketMessage message, ISocketMessageChannel channel);

    }
}
