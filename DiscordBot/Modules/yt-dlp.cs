using Discord.WebSocket;
using DiscordBot.Models;
using DiscordBot.Modules.Interfaces;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;

namespace DiscordBot.Modules
{
    public class YtDlp : IYtDlp
    {
        public YtDlp()
        {

        }

        public List<SongData> GetSongFromSlashCommand(SocketSlashCommand command)
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
                           $"--yes-playlist " +
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

                string jsonString = process.StandardOutput.ReadToEnd().Trim();
                string[] objectsArr = jsonString.Split("\n", StringSplitOptions.RemoveEmptyEntries);

                List<SongData> songlist = new List<SongData>();

                foreach (var jsonObject in objectsArr)
                {
                    using (JsonDocument document = JsonDocument.Parse(jsonObject))
                    {
                        JsonElement root = document.RootElement;

                        SongData song = new SongData
                        {
                            Title = root.GetProperty("title").GetString() ?? "",
                            VideoUrl = root.GetProperty("original_url").GetString() ?? "",
                            AudioUrl = root.GetProperty("url").GetString() ?? "",
                            ThumbnailUrl = root.GetProperty("thumbnail").GetString() ?? "",
                            Duration = TimeSpan.Parse(root.GetProperty("duration_string").GetString() ?? ""),
                            Requester = command.User,
                        };

                        if (song.AudioUrl != "")
                        {
                            songlist.Add(song);
                        }
                    }
                }

                return songlist;
            }
            catch
            {
                throw new Exception($"> [ERROR]: Invalid query in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
            }
        }


    }
}
