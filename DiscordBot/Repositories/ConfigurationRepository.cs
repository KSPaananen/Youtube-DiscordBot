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

        public string GetClientSecret()
        {
            return _config.GetSection("bot:client_secret").Value ?? throw new NullReferenceException();
        }

        public string GetPrefix()
        {
            return _config.GetSection("bot:prefix").Value ?? throw new NullReferenceException();
        }


        public string GetAppID()
        {
            return _config.GetSection("bot:app_id").Value ?? throw new NullReferenceException();
        }

        public string GetBotToken()
        {
            return _config.GetSection("bot:bot_token").Value ?? throw new NullReferenceException();
        }

        public string GetPublicKey()
        {
            return _config.GetSection("bot:public_key").Value ?? throw new NullReferenceException();
        }

        public string GetDiscordLink()
        {
            return _config.GetSection("discord:link").Value ?? "";
        }

    }
}
