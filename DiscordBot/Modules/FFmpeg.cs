using DiscordBot.Modules.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            return process.StandardOutput.BaseStream;
        }


    }
}
