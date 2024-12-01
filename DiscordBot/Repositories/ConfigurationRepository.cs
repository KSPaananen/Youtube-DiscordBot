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
            return _config.GetSection("app:client_secret").Value ?? throw new NullReferenceException();
        }

        public string GetPrefix()
        {
            return _config.GetSection("app:prefix").Value ?? throw new NullReferenceException();
        }


        public string GetAppID()
        {
            return _config.GetSection("app:app_id").Value ?? throw new NullReferenceException();
        }

        public string GetBotToken()
        {
            return _config.GetSection("app:bot_token").Value ?? throw new NullReferenceException();
        }

        public string GetPublicKey()
        {
            return _config.GetSection("app:public_key").Value ?? throw new NullReferenceException();
        }

    }
}
