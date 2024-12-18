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

        public Task HandleJoinGuild(SocketGuild guild)
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

        public Task UserVoiceStateUpdated(SocketUser user, SocketVoiceState channelLeft, SocketVoiceState channelJoined)
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
                    // On bot disconnect dispose resources tied to guild id
                    if (user.Id == _client.CurrentUser.Id && channelLeft.VoiceChannel is SocketGuildChannel socketChannel)
                    {
                        await _musicService.DisposeGuildResourcesAsync(socketChannel.Guild.Id);
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
