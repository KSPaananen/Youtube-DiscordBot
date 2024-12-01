namespace DiscordBot.Repositories.Interfaces
{
    public interface IConfigurationRepository
    {
        string GetName();

        string GetPrefix();

        string GetAppID();

        string GetPublicKey();

        string GetBotToken();
    }
}
