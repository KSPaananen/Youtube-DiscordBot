using DiscordBot.Modules.Interfaces;
using System.Diagnostics;

namespace DiscordBot.Modules
{
    public class FFmpeg : IFFmpeg
    {
        public FFmpeg()
        {

        }

        public Stream GetAudioStreamFromUrl(string url)
        {
            // Keep RedirectStandardOutput & RedirectStandardError set as 'true' or it will not work
            var process = new Process
            {
                // Additional possibly beneficial arguments
                // -re : Runs stream at realtime
                // -hide_banner -loglever error : Quiets log output
                StartInfo = new ProcessStartInfo
                {   
                    FileName = "ffmpeg",
                    Arguments = $"-user_agent \"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36\" " +
                                $"-re " +
                                $"-i \"{url}\" " +
                                $"-ac 2 " +
                                $"-ar 48000 " +
                                $"-f s16le pipe:1",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true, 
                    UseShellExecute = false,
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    Console.WriteLine($"FFmpeg: {args.Data}");
                }
            };

            process.Start();

            process.BeginErrorReadLine();

            return process.StandardOutput.BaseStream;
        }


    }
}
