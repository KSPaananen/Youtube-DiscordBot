using DiscordBot.Middleware.Interfaces;
using System;

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
                Console.WriteLine($"> {ex.Message}");
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
                Console.WriteLine($"> {ex.Message}");

                return default!;
            }
        }


    }
}
