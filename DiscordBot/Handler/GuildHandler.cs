using Discord.WebSocket;
using DiscordBot.Handler.Interfaces;
using DiscordBot.Services.Interfaces;
using System.Reflection;

namespace DiscordBot.Handler
{
    public class GuildHandler : IGuildHandler
    {
        private IMessageService _messageService;

        public GuildHandler(IMessageService messageService)
        {
            _messageService = messageService ?? throw new NullReferenceException(nameof(messageService));
        }

        public Task HandleJoinGuild(SocketGuild guild)
        {
            if (guild is not SocketGuild)
            {
                Console.WriteLine($"SocketGuild was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");

                return Task.CompletedTask;
            }

            // Run inside a task to avoid "handler is blocking the gateway task" errors
            _ = Task.Run(async () =>
            {
                try
                {
                    await _messageService.SendJoinedGuildMessage(guild);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message != null ? $"[ERROR]: {ex.Message}" : $"[ERROR]: Something went wrong in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                }
            });

            return Task.CompletedTask;
        }


    }
}
