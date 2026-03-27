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
        }

        public void Initialize()
        {
            if (discord != null) return;

			try
            {
                discord = new Discord.Discord(clientId, (UInt64)Discord.CreateFlags.NoRequireDiscord);

			}
            catch (Discord.ResultException)
            {
				Console.ForegroundColor = ConsoleColor.DarkMagenta;
				Console.WriteLine("Discord client not detected #2. Retrying in 10 seconds...");
				Console.ResetColor();
				discord = null;
            }
        }

        public bool UpdateDiscordPresence(Dictionary<string, List<string>> gameData)
        {
			if (gameData == null) return false;

			try
            {
                if (discord == null)
                {
					//Console.ForegroundColor = ConsoleColor.DarkMagenta;
					//Console.WriteLine("Discord client not detected #3");
					//Console.ResetColor();
					return false;
                }
                var activityManager = discord.GetActivityManager();
                if (activityManager == null) return false;

				//prepare info
				string GetValue(string key, string def = "")
				{
					if (gameData != null && gameData.TryGetValue(key, out var list) && list.Count > 0)
					{
						return list[0] ?? def;
					}
					return def;
				}

				string date = DateTime.TryParse(GetValue("date"), out var d) ? d.ToString("yyyy") : "Unknown Date";
                string countryName = GetValue("displayed_country_name", "Unknown Country");
                string age = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(GetValue("current_age"));
                string ruler = GetValue("king_name", "Unknown Ruler");
                string governmentRank = !string.IsNullOrEmpty(GetValue("government_rank")) ? (GetValue("government_rank") + " of") : "";

                string warInfo = "";
                if (gameData.ContainsKey("at_war") && gameData["at_war"].Count > 0)
                {
					var enemy = gameData["at_war"][0];
                    int othersCount = 0;
                    int.TryParse(gameData["at_war_others_count"].FirstOrDefault(), out othersCount);

					var others = (othersCount > 0) ? (othersCount > 1 ? $" + {othersCount} others" : $" + 1 other") : "";
					warInfo = $" at war with: {enemy}{others}";
				}

				string detailsText = $"{governmentRank} {countryName}{warInfo} | Year: {date} | {age} | {ruler}";

				string playerTag = GetValue("player");
                string flagAsset = CountriesWithFlag.Contains(playerTag) ? playerTag : "eu4_logo-512";

				var activity = new Discord.Activity
                {
					Type = ActivityType.Playing,

					State = "By Karol115",

					Details = detailsText,

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
						Console.ForegroundColor = ConsoleColor.Green;
						Console.WriteLine("Updated Discord presence");
						Console.ResetColor();
					}
                    else
                    {
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine("Updating Discord presence failed: \n" + result);
						Console.ResetColor();
					}
                });

                discord.RunCallbacks();

                Console.WriteLine(activity.Details);
                return true;
            }
            catch (Exception)
            {
				Console.ForegroundColor = ConsoleColor.DarkMagenta;
				Console.WriteLine("Discord client not detected #4");
				Console.ResetColor();

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
