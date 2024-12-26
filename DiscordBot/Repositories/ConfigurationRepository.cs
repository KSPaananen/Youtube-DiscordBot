using DiscordBot.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;

namespace DiscordBot.Repositories
{
    public class ConfigurationRepository : IConfigurationRepository
    {
        private IConfiguration _config;

        public ConfigurationRepository(IConfiguration config)
        {
            _config = config ?? throw new NullReferenceException(nameof(config));
        }

        public string GetBotToken()
        {
            return _config.GetSection("Bot:Token").Value ?? throw new NullReferenceException();
        }


    }
}
