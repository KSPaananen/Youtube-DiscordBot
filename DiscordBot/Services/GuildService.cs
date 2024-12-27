using Discord;
using Discord.WebSocket;
using DiscordBot.Extensions;
using DiscordBot.Services.Interfaces;

namespace DiscordBot.Services
{
    public class GuildService : IGuildService
    {
        private DiscordSocketClient _client;

        public GuildService(DiscordSocketClient client)
        {
            _client = client ?? throw new NullReferenceException(nameof(client));
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
                    Value = $"- `/play`\n" +
                            $"- `/stop-playing`\n" +
                            $"- `/skip-song`\n" +
                            $"- `/clear-queue`\n",
                    IsInline = false
                },
            };

            embedBuilder.WithDefaults();

            await guild.SystemChannel.SendMessageAsync(embeds: new[] { embedBuilder.Build() });

            return;
        }


    }
}
