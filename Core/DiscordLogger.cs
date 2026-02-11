using System;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using vatsys;

namespace MumbleReconnect
{
    internal static class DiscordLogger
    {
        private const string WebhookUrl = "https://discord.com/api/webhooks/1462706456381100052/RtUMW4K29oRSbuB4jcUjaTY5DHCE8arkE28oyR31jb7kWtA32yz7f7PRtObdFUSYhKkM";

        public static async void LogMumbleError(string message, Exception ex = null)
        {
            try
            {
                await LogToDiscordAsync("Mumble Error", message, 15158332, ex);
            }
            catch
            {
                // Silently fail if Discord logging fails - we don't want to create a loop
            }
        }

        public static async void LogMumbleReconnect(string message)
        {
            try
            {
                await LogToDiscordAsync("Mumble Reconnected", message, 3066993, null); // Green color
            }
            catch
            {
                // Silently fail if Discord logging fails - we don't want to create a loop
            }
        }

        private static async Task LogToDiscordAsync(string title, string message, int color, Exception ex = null)
        {
            try
            {
                var cid = GetCID();
                var callsign = Network.IsConnected && Network.Me != null ? Network.Me.Callsign : "Not connected";

                var embed = new
                {
                    embeds = new[]
                    {
                        new
                        {
                            title = title,
                            description = message,
                            color = color,
                            fields = ex != null ? new[]
                            {
                                new
                                {
                                    name = "CID",
                                    value = cid,
                                    inline = true
                                },
                                new
                                {
                                    name = "Callsign",
                                    value = callsign,
                                    inline = true
                                },
                                new
                                {
                                    name = "Exception Type",
                                    value = ex.GetType().Name,
                                    inline = true
                                },
                                new
                                {
                                    name = "Exception Message",
                                    value = ex.Message ?? "No message",
                                    inline = false
                                },
                                new
                                {
                                    name = "Stack Trace",
                                    value = (ex.StackTrace?.Length > 1024 ? ex.StackTrace.Substring(0, 1024) + "..." : ex.StackTrace) ?? "No stack trace",
                                    inline = false
                                }
                            } : new[]
                            {
                                new
                                {
                                    name = "CID",
                                    value = cid,
                                    inline = true
                                },
                                new
                                {
                                    name = "Callsign",
                                    value = callsign,
                                    inline = true
                                }
                            },
                            timestamp = DateTime.UtcNow.ToString("o"),
                            footer = new
                            {
                                text = "Mumble Reconnect"
                            }
                        }
                    }
                };

                var json = JsonConvert.SerializeObject(embed);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var response = await client.PostAsync(WebhookUrl, content);
                    // We don't need to check the response - just fire and forget
                }
            }
            catch
            {
                // Silently fail - we don't want Discord logging to cause issues
            }
        }

        private static string GetCID()
        {
            try
            {
                var configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);

                if (!configuration.HasFile) return "Unknown";

                if (!File.Exists(configuration.FilePath)) return "Unknown";

                var config = File.ReadAllText(configuration.FilePath);

                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(config);

                var cidString = string.Empty;

                foreach (XmlNode childNode in xmlDocument.DocumentElement.SelectSingleNode("userSettings").SelectSingleNode("vatsys.Properties.Settings").ChildNodes)
                {
                    if (childNode.Attributes.GetNamedItem("name").Value == "VATSIMID")
                    {
                        cidString = childNode.InnerText;
                        break;
                    }
                }

                var success = int.TryParse(cidString, out var cid);

                return success ? cid.ToString() : "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
