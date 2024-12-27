using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Extensions
{
    public static class ComponentBuilderExtensions
    {
        /// <summary>
        ///     <para>Overwrite certain features with set default values.</para>
        /// </summary>
        /// <param name="builder"></param>
        public static ComponentBuilder WithDefaults(this ComponentBuilder builder)
        {
           

            return builder;
        }


    }
}
