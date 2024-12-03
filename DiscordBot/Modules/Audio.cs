using DiscordBot.Modules.Interfaces;
using System.Diagnostics;

namespace DiscordBot.Modules
{
    public class Audio : IAudio
    {
        public Audio()
        {

        }

        public string GetAudioUrlFromLink(string link)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = $"--quiet -f bestaudio -g \"{link}\"",
                    //Arguments = $"--quiet --no-warnings -f bestaudio -g \"{url}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();

            string audioUrl = process.StandardOutput.ReadToEnd().Trim();

            return audioUrl;
        }

        public Process GetAudioStreamFromUrl(string url)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{url}\" -ac 2 -ar 48000 -f s16le pipe:1",
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

            return process;
        }


    }
}
