using System.IO.Compression;

namespace EU4_RPC
{
    public static class GetGameInfo
    {
        enum GovernmentRank
        {
            None = 0,
            Duchy = 1,
            Kingdom = 2,
            Empire = 3,
        }

        public static Dictionary<string, List<string>> ReadSaveGame(string filePath)
        {
            var gameData = new Dictionary<string, List<string>>
            {
                ["date"] = new(),
                ["player"] = new(),
                ["displayed_country_name"] = new(),
                ["current_age"] = new(),
                ["king_name"] = new(),
				["is_regency"] = new(),
                ["government_rank"] = new(),
                ["at_war"] = new(),
				["at_war_others_count"] = new(),
			};

			try
			{
				using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					byte[] header = new byte[2];
					fs.Read(header, 0, 2);
					fs.Position = 0;

					if (header[0] == 'P' && header[1] == 'K')
					{
						using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
						{
							var metaEntry = archive.GetEntry("meta");
							if(metaEntry != null)
							{
								using (Stream s = metaEntry.Open())
									ParseStream(s, gameData, true);
							}

							var stateEntry = archive.GetEntry("gamestate");
							if (stateEntry != null)
							{
								using (Stream s = stateEntry.Open())
									ParseStream(s, gameData);
							}
						}
					}
					else
					{
						ParseStream(fs, gameData);
					}
				}
			}
			catch(Exception ex)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"[Parser Error] Could not read {Path.GetFileName(filePath)}: {ex.Message}");
				Console.ResetColor();
			}
            
            return gameData;
        }

		static string line = "";
		static string tline = "";
		static int linesScanned = 0;

		static string playerTag = "";

		static bool inCountriesSection = false;

		static bool inPlayerCountry = false;
		static int playerCountryDepth = 0;

		static bool inMonarchBlock = false;
		static int monarchBlockDepth = 0;

		static string monarchTempName = "";
		static string dynastyTempName = "";

		// wars
		static bool inActiveWar = false;
		static List<string> attackers = new List<string>();
		static List<string> defenders = new List<string>();
		static int warBlockDepth = 0;

		private static void ParseStream(Stream stream, Dictionary<string, List<string>> gameData, bool onlyMeta = false)
		{
			ResetValues();

			using var reader = new StreamReader(stream, System.Text.Encoding.GetEncoding(1252));

			playerTag = gameData["player"].FirstOrDefault() ?? "";

			while ((line = reader.ReadLine()) != null)
			{
				linesScanned++;

				tline = line.Trim();
				if (string.IsNullOrEmpty(line)) continue;

				// main info
				if (tline.Contains("="))
				{
					var parts = tline.Split('=', 2);
					string key = parts[0].Trim();
					if (key == "date" || key == "player" || key == "current_age")
					{
						string val = parts[1].Trim().Trim('"').Replace("_", " ");
						if (gameData[key].Count == 0)
						{
							Set(gameData, key, val);
							if (key == "player")
							{
								playerTag = val;
								Set(gameData, "displayed_country_name", CountriesTags.CountryTagsToNames.GetValueOrDefault(val, val));
							}
						}

						if (onlyMeta && gameData["date"].Count > 0 && gameData["player"].Count > 0)
						{
							if (linesScanned > 100) return;
						}
					}
				}


				if (onlyMeta) continue;

				// Country Start & End
				if (tline == "countries={") { inCountriesSection = true; continue; }
				if (inCountriesSection && tline == "provinces={") { inCountriesSection = false; continue; }

				// Player Tag
				if (inCountriesSection && !inPlayerCountry)
				{
					if (tline.StartsWith(playerTag + "={"))
					{
						string next = reader.ReadLine()?.Trim();
						if (next == "human=yes") { inPlayerCountry = true; playerCountryDepth = 1; }
					}
					//continue;
				}

				if (inPlayerCountry)
				{
					// Country Navigation
					playerCountryDepth += GetDepthChange(tline);

					// Monarch
					if (inMonarchBlock)
					{
						HandleMonarchLine(gameData);
						if (monarchBlockDepth <= 0) inMonarchBlock = false;
					}

					// Monarch & Gov
					if(tline.StartsWith("monarch") && tline.EndsWith("{"))
					{
						Set(gameData, "is_regency", tline.Contains("consort") ? "true" : "false");
						inMonarchBlock = true;
						monarchBlockDepth = 1;
						monarchTempName = "";
						dynastyTempName = "";
					}
					else if (tline.StartsWith("government_rank="))
					{
						var rank = (GovernmentRank)int.Parse(tline.Split('=')[1]);
						Set(gameData, "government_rank", rank.ToString());
					}

					if (playerCountryDepth <= 0 || tline.StartsWith("government_reform_progress="))
					{
						inPlayerCountry = false;
						playerCountryDepth = 0;
					}

					continue;
				}

				// wars
				if (tline.StartsWith("active_war={"))
				{
					inActiveWar = true;
					warBlockDepth = 1;
					attackers.Clear();
					defenders.Clear();
				}
				else if (inActiveWar)
				{
					warBlockDepth += GetDepthChange(tline);

					CheckForActiveWars(gameData);
					continue;
				}
			}
		}
		

		private static void Set(Dictionary<string, List<string>> dict, string key, string val)
		{
			if (!dict.ContainsKey(key)) return;
			dict[key].Clear();
			dict[key].Add(val);
		}

		private static int GetDepthChange(string line)
		{
			return line.Count(f => f == '{') - line.Count(f => f == '}');
		}

		private static void ResetValues()
		{
			linesScanned = 0;
			inCountriesSection = false;
			inPlayerCountry = false;
			playerCountryDepth = 0;
			inMonarchBlock = false;
			monarchBlockDepth = 0;
			inActiveWar = false;
			attackers.Clear();
			defenders.Clear();
			warBlockDepth = 0;
			monarchTempName = "";
			dynastyTempName = "";
		}

		private static void HandleMonarchLine(Dictionary<string, List<string>> gameData)
		{
			monarchBlockDepth += GetDepthChange(tline);

			if (monarchBlockDepth == 1)
			{
#if DEBUG
				Console.WriteLine($"{monarchBlockDepth} : {tline}");
#endif
				if (tline.StartsWith("name=\"")) monarchTempName = tline.Split('"')[1];
				else if (tline.StartsWith("dynasty=\"")) dynastyTempName = tline.Split('"')[1];
				else if(tline.StartsWith("succeeded=yes"))
				{
					string fullName = string.IsNullOrEmpty(dynastyTempName) ? monarchTempName : $"{monarchTempName} {dynastyTempName}";
					Set(gameData, "king_name", fullName);
				}
			}
		}

		private static void CheckForActiveWars(Dictionary<string, List<string>> gameData)
		{
			if (tline.StartsWith("add_attacker=")) attackers.Add(tline.Split('"')[1]);
			else if (tline.StartsWith("add_defender=")) defenders.Add(tline.Split('"')[1]);
			else if (warBlockDepth <= 0)
			{
				inActiveWar = false;
				ProcessWar(gameData);
			}
		}

		private static void ProcessWar(Dictionary<string, List<string>> gameData)
		{
			bool isAttacker = attackers.Contains(playerTag);
			bool isDefender = defenders.Contains(playerTag);

			if (isAttacker || isDefender)
			{
				var enemies = isAttacker ? defenders : attackers;
				if (enemies.Count > 0)
				{
					if (gameData["at_war"].Count == 0)
					{
						string leaderTag = enemies[0];
						string leaderName = CountriesTags.CountryTagsToNames.GetValueOrDefault(leaderTag, leaderTag);
						gameData["at_war"].Add(leaderName);

						int othersInThisWar = enemies.Count - 1;
						UpdateOthersCount(gameData, othersInThisWar);
					}
					else
					{
						UpdateOthersCount(gameData, enemies.Count);
					}
				}
			}
		}

		private static void UpdateOthersCount(Dictionary<string, List<string>> gameData, int countToAdd)
		{
			if (countToAdd <= 0) return;

			int currentTotal = 0;
			if (gameData["at_war_others_count"].Count > 0)
			{
				int.TryParse(gameData["at_war_others_count"][0], out currentTotal);
			}

			Set(gameData, "at_war_others_count", (currentTotal + countToAdd).ToString());
		}
	}
}
