using Discord;

namespace DiscordBot.Extensions
{
    public static class EmbedBuilderExtensions
    {
        /// <summary>
        ///     <para>Overwrite certain features with set default values.</para>
        /// </summary>
        /// <param name="builder"></param>
        public static EmbedBuilder WithDefaults(this EmbedBuilder builder, EmbedFooterBuilder? footerBuilder = null)
        {
            if (builder.Author != null)
            {
                builder.Author.IconUrl = null;
            }

            builder.Color = new Color(1f, 0.984f, 0f);
            builder.Timestamp = footerBuilder != null ?  DateTime.UtcNow : null;
            builder.Footer = footerBuilder != null ? footerBuilder : null;

            return builder;
        }


    }
}
