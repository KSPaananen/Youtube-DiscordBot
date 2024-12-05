namespace DiscordBot.Middleware.Interfaces
{
    public interface IErrorHandlerMiddleware
    {
        void Execute(Action act);

        T Execute<T>(Func<T> func);

    }
}
