using Discord.WebSocket;
using DiscordBot.Handler.Interfaces;
using DiscordBot.Services.Interfaces;
using System.Reflection;

namespace DiscordBot.Handler
{
    public class GuildHandler : IGuildHandler
    {
        private DiscordSocketClient _client;

        private IMessageService _messageService;
        private IMusicService _musicService;

        public GuildHandler(DiscordSocketClient client, IMessageService messageService, IMusicService musicService)
        {
            _client = client ?? throw new NullReferenceException(nameof(client));

            _messageService = messageService ?? throw new NullReferenceException(nameof(messageService));
            _musicService = musicService ?? throw new NullReferenceException(nameof(musicService));
        }

        public Task HandleJoinedGuild(SocketGuild guild)
        {
            if (guild == null)
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
                    Console.WriteLine(ex.Message != null ? $"> [ERROR]: {ex.Message}" : $"[ERROR]: Something went wrong in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                }
            });

            return Task.CompletedTask;
        }

        public Task HandleUserBanned(SocketUser user, SocketGuild server)
        {
            return Task.CompletedTask;
        }

        public Task HandleUserJoined(SocketGuildUser user)
        {
            return Task.CompletedTask;
        }

        public Task HandleUserLeft(SocketGuild server, SocketUser user)
        {
            return Task.CompletedTask;
        }

        public Task HandleUserUnBanned(SocketUser user, SocketGuild server)
        {
            return Task.CompletedTask;
        }

        public Task HandleUserVoiceStateUpdated(SocketUser user, SocketVoiceState disconnectedChannelState, SocketVoiceState connectedChannelState)
        {
            if (user == null)
            {
                Console.WriteLine($"SocketUser was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");

                return Task.CompletedTask;
            }

            // Run inside a task to avoid "handler is blocking the gateway task" errors
            _ = Task.Run(async () =>
            {
                try
                {
                    // On leave
                    if (disconnectedChannelState.VoiceChannel is SocketVoiceChannel disconnectedChannel)
                    {
                        // On bot disconnect dispose resources tied to guild in IMusicService
                        if (user.Id == _client.CurrentUser.Id)
                        {
                            await _musicService.DisposeGuildResourcesAsync(disconnectedChannel.Guild.Id);
                        }
                        // On user disconnect check if channel has any users left
                        else if (user.Id != _client.CurrentUser.Id)
                        {
                            await _musicService.CheckChannelStateAsync(disconnectedChannel);
                        }
                    }

                    // On join
                    if (connectedChannelState.VoiceChannel is SocketGuildChannel connectedChannel)
                    {

                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message != null ? $"> [ERROR]: {ex.Message}" : $"[ERROR]: Something went wrong in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                }
            });

            return Task.CompletedTask;
        }


    }
}
