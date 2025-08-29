using Discord;
using System.Globalization;

namespace EU4_RPC
{
    public class RPC
    {
        public Discord.Discord discord;

        private const long clientId = 1409886197303476258;

        public RPC()
        {
            Initialize();
        }

        public void Initialize()
        {
            discord = null;
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

                string countryName = (gameData.ContainsKey("displayed_country_name") && gameData["displayed_country_name"].Count > 0) ? gameData["displayed_country_name"][0] : "Unknown Country";
                string date = (gameData.ContainsKey("date") && gameData["date"].Count > 0) ? DateTime.Parse(gameData["date"][0]).ToString("yyyy") : "Unknown Date";
                string ruler = (gameData.ContainsKey("localization") && gameData["localization"].Count > 1) ? gameData["localization"][1] : "Unknown Ruler";
                string atWar = (gameData.ContainsKey("at_war") && gameData["at_war"].Count > 0) ? (" at war with " + gameData["at_war"][0]) : "";
                string age = (gameData.ContainsKey("current_age") && gameData["current_age"].Count > 0) ? (CultureInfo.CurrentCulture.TextInfo.ToTitleCase(gameData["current_age"][0])) : "";
                string governmentRank = (gameData.ContainsKey("government_rank") && gameData["government_rank"].Count > 0) ? (" " + gameData["government_rank"][0] + " of") : "";

                var activity = new Discord.Activity
                {
                    Type = ActivityType.Playing,

                    State = "By Karol115",
                    Details = $"Playing as:{governmentRank} {countryName} {atWar}| Year: {date} | {age} | {ruler}",
                    /*Timestamps =
                    {
                        Start = DateTimeOffset.Now.ToUnixTimeSeconds()
                    },*/
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
                        Console.WriteLine("send");
                    }
                    else
                    {
                        Console.WriteLine("could't send\n" + result);
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
