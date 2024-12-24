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

        public List<SongData> GetSongFromSlashCommand(SocketSlashCommand command)
        {
            try
            {
                // Extract query from slash commands first parameter
                if (command.Data.Options.First().Value.ToString() is not string query)
                {
                    throw new Exception($"> [ERROR]: SocketSlashCommands first parameter was null in {this.GetType().Name} : {MethodBase.GetCurrentMethod()!.Name}");
                }

                string args = $"--quiet " +
                              $"--no-warnings " +
                              $"--dump-json " +
                              $"-N 5 " +
                              $"--extractor-args \"youtube:skip=dash,unavailable_videos\" " +
                              $"--skip-download " +
                              $"--match-filter \"duration > 60\" " + // Minimum minute long
                              $"-f bestaudio[ext=m4a] ";

                // Modify arguments depending if we received a link or a query
                if (query.Contains("youtube.com") || query.Contains("youtu.be"))
                {
                    args += $"--yes-playlist " +
                            $"--concurrent-fragments 10 " +
                            $"--playlist-items 1-25 " +
                            $"\"{query}\"";
                }
                else if (!query.Contains("https://"))
                {
                    args += $"\"ytsearch:{query}\"";
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
                            Title = root.TryGetProperty("title", out JsonElement titleElement) ? titleElement.GetString() ?? "" : "",
                            VideoUrl = root.TryGetProperty("original_url", out JsonElement videoUrlElement) ? videoUrlElement.GetString() ?? "" : "",
                            AudioUrl = root.TryGetProperty("url", out JsonElement audioUrlElement) ? audioUrlElement.GetString() ?? "" : "",
                            ThumbnailUrl = root.TryGetProperty("thumbnail", out JsonElement thumbnailElement) ? thumbnailElement.GetString() ?? "" : "",
                            Duration = TimeSpan.Parse(root.TryGetProperty("duration_string", out JsonElement durationElement) ? durationElement.GetString() ?? "" : ""),
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
