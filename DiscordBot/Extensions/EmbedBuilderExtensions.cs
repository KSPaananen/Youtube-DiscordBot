using Discord;

namespace DiscordBot.Extensions
{
    public static class EmbedBuilderExtensions
    {
        /// <summary>
        ///     <para>Overwrite certain features with set default values.</para>
        /// </summary>
        /// <param name="builder"></param>
        public static EmbedBuilder WithDefaults(this EmbedBuilder builder, EmbedFooterBuilder footerBuilder)
        {
            if (builder.Author != null)
            {
                builder.Author.IconUrl = null;
            }

            builder.Color = new Color(1f, 0.984f, 0f);
            builder.Timestamp = DateTime.UtcNow;
            builder.Footer = footerBuilder;

            return builder;
        }


    }
}
