using DiscordBot.Middleware.Interfaces;

namespace DiscordBot.Middleware
{
    public class ErrorHandlerMiddleware : IErrorHandlerMiddleware
    {
        public ErrorHandlerMiddleware()
        {

        }

        public void Execute(Action act)
        {
            try
            {
                act();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"> [ERROR] {ex.Message}");
            }
        }

        public T Execute<T>(Func<T> func)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"> [ERROR] {ex.Message}");

                return default!;
            }
        }


    }
}
