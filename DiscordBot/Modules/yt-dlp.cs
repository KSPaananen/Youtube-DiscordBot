using Discord.WebSocket;
using DiscordBot.Models;
using DiscordBot.Modules.Interfaces;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace DiscordBot.Modules
{
    public class YtDlp : IYtDlp
    {
        public YtDlp()
        {

        }

        public Song GetSongFromSlashCommand(SocketSlashCommand command)
        {
            try
            {
                // Extract query from slash commands first parameter
                if (command.Data.Options.First().Value.ToString() is not string query)
                {
                    throw new Exception($"> [ERROR]: SocketSlashCommands first parameter was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                }

                string args = $"";

                if (query.Contains("https://") && (query.Contains("youtube.com") || query.Contains("youtu.be")))
                {
                    args = $"--quiet " +
                           $"--no-warnings " +
                           $"--dump-json " +
                           $"-f bestaudio[ext=m4a] " +
                           $"\"{query}\"";
                }
                else
                {
                    args = $"--quiet " +
                           $"--no-warnings " +
                           $"--dump-json " +
                           $"-f bestaudio[ext=m4a] " +
                           $"\"ytsearch:{query}\"";
                }

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

                using (JsonDocument document = JsonDocument.Parse(process.StandardOutput.ReadToEnd().Trim()))
                {
                    JsonElement root = document.RootElement;

                    Song song = new Song
                    {
                        Title = root.GetProperty("title").GetString() ?? "",
                        VideoUrl = root.GetProperty("original_url").GetString() ?? "",
                        AudioUrl = root.GetProperty("url").GetString() ?? "",
                        ThumbnailUrl = root.GetProperty("thumbnail").GetString() ?? "",
                        Duration = TimeSpan.Parse(root.GetProperty("duration_string").GetString() ?? ""),
                        Requester = command.User,
                    };

                    return song;
                }

            }
            catch
            {
                throw new Exception($"[ERROR]: Invalid query in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }
        }


    }
}
