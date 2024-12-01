namespace DiscordBot.Repositories.Interfaces
{
    public interface IConfigurationRepository
    {
        string GetClientSecret();

        string GetPrefix();

        string GetAppID();

        string GetPublicKey();

        string GetBotToken();
    }
}
