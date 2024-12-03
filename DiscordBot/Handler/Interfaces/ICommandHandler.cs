using Discord;
using Discord.WebSocket;

namespace DiscordBot.Handler.Interfaces
{
    public interface ICommandHandler
    {
        Task HandleSlashCommandAsync(SocketSlashCommand command);

        Task HandleMessageReceivedAsync(SocketMessage message);

        Task HandleReactionAddedAsync(Cacheable<IUserMessage, ulong> cacheable1, Cacheable<IMessageChannel, ulong> cacheable2, SocketReaction reaction);

        Task HandleButtonExecuted(SocketMessageComponent component);

    }
}
