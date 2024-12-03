namespace DiscordBot.Handler.Interfaces
{
    public interface IErrorHandler
    {
        void Execute(Action act);

        T Execute<T>(Func<T> func);

    }
}
