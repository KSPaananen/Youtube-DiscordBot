using DiscordBot.Models;
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
                // Additional possibly beneficial arguments
                // -loglever error : Quiets log output
                // -re : Realtime streaming
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-headers \"User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:128.0) Gecko/20100101 Firefox/128.0\\r\\n\" " +
                                $"-headers \"Connection: keep-alive\\r\\n\" " +
                                $"-protocol_whitelist file,http,https,tls,tcp " +
                                $"-fflags +nobuffer " +
                                $"-flags low_delay " +
                                $"-reconnect 1 " +
                                $"-reconnect_at_eof 1 " +
                                $"-reconnect_streamed 1 " +
                                $"-reconnect_delay_max 3 " +
                                $"-rw_timeout 5000000 " +
                                $"-timeout 1000000 " +
                                $"-max_interleave_delta 0 " +
                                $"-hide_banner " +
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
