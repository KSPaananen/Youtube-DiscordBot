using Discord;
using Discord.WebSocket;
using DiscordBot.Extensions;
using DiscordBot.Models;
using DiscordBot.Repositories.Interfaces;
using DiscordBot.Services.Interfaces;

namespace DiscordBot.Services
{
    public class MessageService : IMessageService
    {
        private DiscordSocketClient _client;

        private IConfigurationRepository _configurationRepository;

        private string _discordLink;

        public MessageService(DiscordSocketClient client, IConfigurationRepository configurationRepository)
        {
            _client = client ?? throw new NullReferenceException(nameof(client));

            _configurationRepository = configurationRepository ?? throw new NullReferenceException(nameof(configurationRepository));

            _discordLink = _configurationRepository.GetDiscordLink();
        }

        public async Task SendJoinedGuildMessage(SocketGuild guild)
        {
            var embedBuilder = new EmbedBuilder();
            var componentBuilder = new ComponentBuilder();

            embedBuilder.Author = new EmbedAuthorBuilder
            {
                IconUrl = guild.IconUrl,
                Name = $"{guild.CurrentUser.Username} has arrived to {guild.Name}",
                Url = null
            };
            embedBuilder.Description = $"Heres some basic information about me: ";
            embedBuilder.ThumbnailUrl = _client.CurrentUser.GetAvatarUrl();
            embedBuilder.Fields = new List<EmbedFieldBuilder>
            {
                new EmbedFieldBuilder
                {
                    Name = "Commands" ,
                    Value = $"- `/play` allows you to play music with search queries or links" +
                            $"\n\n - /skip skips the currently playing song" + 
                            $"\n\n - /clear-queue skips the currently playing song",
                    IsInline = false
                },
            };

            if (_discordLink  != "")
            {
                embedBuilder.Fields.Add(new EmbedFieldBuilder
                {
                    Name = $"Discord server",
                    Value = $"Join the developers [Discord server]({_discordLink}) to receive support, report bugs or suggest new features.",
                    IsInline = true
                });
            }

            embedBuilder.WithDefaults();

            await guild.SystemChannel.SendMessageAsync(embeds: new[] { embedBuilder.Build() });

            return;
        }

        
    }
}
