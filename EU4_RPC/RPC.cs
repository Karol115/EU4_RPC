using Discord;
using System.Globalization;

namespace EU4_RPC
{
    public class RPC
    {
        public Discord.Discord discord;

        private const long clientId = 1409886197303476258;
        private long startTimestamp;

        public RPC()
        {
            startTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            Initialize();
        }

        public void Initialize()
        {
            while (discord == null)
            {
                try
                {
                    discord = new Discord.Discord(clientId, (UInt64)Discord.CreateFlags.NoRequireDiscord);
                }
                catch (Discord.ResultException)
                {
                    Console.WriteLine("Discord client not detected #2. Retrying in 10 seconds...");
                    Thread.Sleep(10000);
                }
            }

            UpdateDiscordPresence(Program.saveGameDict);
        }

        public bool UpdateDiscordPresence(Dictionary<string, List<string>> gameData)
        {
            try
            {
                if (discord == null)
                {
                    Console.WriteLine("Discord client not detected #3");
                    return false;
                }
                var activityManager = discord.GetActivityManager();
                if (activityManager == null)
                    return false;

                //prepare info
                string GetValue(string key, string def = "") =>
                    (gameData.ContainsKey(key) && gameData[key].Count > 0) ? gameData[key][0] : def;

                string date = DateTime.TryParse(GetValue("date"), out var d) ? d.ToString("yyyy") : "Unknown Date";
                string countryName = GetValue("displayed_country_name", "Unknown Country");
                string age = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(GetValue("current_age"));
                string ruler = GetValue("king_name", "Unknown Ruler");
                string governmentRank = !string.IsNullOrEmpty(GetValue("government_rank")) ? " " + GetValue("government_rank") + " of" : "";

                string atWar = "";
                if (gameData.ContainsKey("at_war") && gameData["at_war"].Count > 0)
                {
                    var count = gameData["at_war"].Count;
                    atWar = "at war with " + gameData["at_war"][0];
                    if (count > 1) atWar += " and " + (count - 1) + " other(s)";
                    atWar += " ";
                }


                var activity = new Discord.Activity
                {
                    Type = ActivityType.Playing,

                    State = "By Karol115",
                    Details = $"Playing as:{governmentRank} {countryName} {atWar}| Year: {date} | {age} | {ruler}",

                    Timestamps =
                    {
                        Start = startTimestamp
                    },

                    Assets =
                    {
                        LargeImage = "eu4_logo-512",
                        LargeText = "eu4"
                    }
                };

                activityManager.UpdateActivity(activity, (Result result) =>
                {
                    if (result == Discord.Result.Ok)
                    {
                        Console.WriteLine("response");
                    }
                    else
                    {
                        Console.WriteLine("could't response\n" + result);
                    }
                });

                Console.WriteLine(activity.Details);
                return true;
            }
            catch (Exception)
            {
                Console.WriteLine("Discord client not detected #4");
                return false;
            }
        }
    }
}
