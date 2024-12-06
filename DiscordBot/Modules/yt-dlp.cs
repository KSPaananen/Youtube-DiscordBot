﻿using DiscordBot.Modules.Interfaces;
using System.Diagnostics;

namespace DiscordBot.Modules
{
    public class YtDlp : IYtDlp
    {
        public YtDlp()
        {

        }

        public string GetAudioUrlFromLink(string link)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = $"--quiet " +
                                $"--no-warnings " +
                                $"-f bestaudio[ext=m4a] " +
                                $"-g \"{link}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();

            string audioUrl = process.StandardOutput.ReadToEnd().Trim();

            return audioUrl;
        }


    }
}
