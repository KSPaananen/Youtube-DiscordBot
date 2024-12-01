using DiscordBot.Services.Interfaces;
using System;

namespace DiscordBot.Services
{
    public class ErrorHandler : IErrorHandler
    {
        public ErrorHandler()
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
