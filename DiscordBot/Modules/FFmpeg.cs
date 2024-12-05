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
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    // Keep RedirectStandardOutput & RedirectStandardError set as 'true' or it will not work
                    FileName = "ffmpeg",
                    Arguments = $"-re -i \"{url}\" -ac 2 -ar 48000 -f s16le pipe:1", // Add -hide_banner -loglevel error to quiet output
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
