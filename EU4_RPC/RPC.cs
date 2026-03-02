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
            if (discord != null) return;

            try
            {
                discord = new Discord.Discord(clientId, (UInt64)Discord.CreateFlags.NoRequireDiscord);

                /*discord.SetLogHook(Discord.LogLevel.Error, (level, message) =>
                {
                    Console.WriteLine($"Discord Error: {message}");
                });*/

                UpdateDiscordPresence(Program.saveGameDict);

            }
            catch (Discord.ResultException)
            {
                Console.WriteLine("Discord client not detected #2. Retrying in 10 seconds...");
                discord = null;
                //Thread.Sleep(10000);
            }

            //UpdateDiscordPresence(Program.saveGameDict);
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
                string governmentRank = !string.IsNullOrEmpty(GetValue("government_rank")) ? (" " + GetValue("government_rank") + " of") : "";

                string atWarDisplay = "";
                if (gameData.ContainsKey("at_war") && gameData["at_war"].Count > 0)
                {
                    var count = gameData["at_war"].Count;
                    var enemy = gameData["at_war"][0];
                    var others = count > 1 ? $" + {count - 1}" : "";
                    atWarDisplay = $" | ⚔️ {enemy} {others}";
                }

				string playerTag = GetValue("player");
                string flagAsset = CountriesWithFlag.Contains(playerTag) ? playerTag : "eu4_logo-512";

				var activity = new Discord.Activity
                {
					Type = ActivityType.Playing,

					State = "By Karol115",

					Details = $"{governmentRank} {countryName} {atWarDisplay}| Year: {date} | {age} | {ruler}",

					Timestamps = { Start = startTimestamp },

					Assets =
                    {
                        LargeImage = "eu4_logo-512",
                        //SmallImage = flagAsset,
                        LargeText = countryName,
						SmallText = "Europa Universalis IV"
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

        private readonly HashSet<string> CountriesWithFlag = new()
        {
            // --- EUROPE ---
			"ARA", "AUT", "BAV", "BOH", "BRA", "BUR", "BYZ", "CAS", "DAN", "ENG",
	        "FRA", "GBR", "GER", "HAB", "HLR", "HUN", "ITA", "LAN", "LIT", "LVA",
	        "MLO", "MOS", "NAP", "NED", "NOR", "NOV", "PAP", "PLC", "POL", "POR",
	        "PRU", "ROM", "RUS", "SAX", "SCO", "SPA", "SWE", "TEU",

            // --- ASIA ---
            "BAH", "DLH", "JAP", "MNG", "MUG", "PER", "QNG", "TIM", "VIJ",

            // --- AFRICA ---
            "ADU", "EGY", "ETH", "MAM", "MOR", "TUN",

            // --- AMERICA ---
            "BRZ", "CAN", "USA",

            // --- OTHER ---
            "TUR"
		};
    }
}
