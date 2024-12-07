using DiscordBot.Modules.Interfaces;
using System.Diagnostics;
using System.Reflection;

namespace DiscordBot.Modules
{
    public class YtDlp : IYtDlp
    {
        public YtDlp()
        {

        }

        public string GetAudioUrlFromQuery(string query)
        {
            try
            {
                string args = $"";

                if (query.Contains("https://") && (query.Contains("youtube.com") || query.Contains("youtu.be")))
                {
                    args = $"--quiet " +
                           $"--no-warnings " +
                           $"-f bestaudio[ext=m4a] " +
                           $"-g \"{query}\"";
                }
                else
                {
                    args = $"--quiet " +
                           $"--no-warnings " +
                           $"--skip-download " +
                           $"--get-url " +
                           $"-f bestaudio[ext=m4a] " +
                           $"\"ytsearch:{query}\"";
                }

                // Additional possibly beneficial arguments
                // --dump-json => Get all information about found video
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "yt-dlp",
                        Arguments = args,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };

                process.Start();

                string url = process.StandardOutput.ReadToEnd().Trim();

                return url;
            }
            catch
            {
                throw new Exception($"[ERROR]: Invalid query in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }
        }


    }
}
