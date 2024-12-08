using Discord.WebSocket;
using DiscordBot.Handler.Interfaces;
using System.Reflection;

namespace DiscordBot.Handler
{
    public class GuildHandler : IGuildHandler
    {
        private SocketGuildChannel? _systemChannel;

        public GuildHandler()
        {

        }

        public async Task HandleJoinGuildAsync(SocketGuild guild)
        {
            if (guild is not SocketGuild || guild.SystemChannel is not SocketTextChannel systemChannel)
            {
                throw new Exception($"> [ERROR]: SocketGuild was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }

            _systemChannel = systemChannel;

            // Send guild join message to the first channel
            await systemChannel.SendMessageAsync("testing");







            return;
        }


    }
}
