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

		private static void ParseStream(Stream stream, Dictionary<string, List<string>> gameData, bool onlyMeta = false)
		{
			int linesScanned = 0;

			using (StreamReader reader = new StreamReader(stream, System.Text.Encoding.GetEncoding(1252)))
			{
				string line;
				string playerTag = gameData["player"].FirstOrDefault() ?? "";
				bool inCountriesSection = false;
				bool inPlayerCountry = false;
				int playerCountryDepth = 0;
				bool inMonarchBlock = false;
				int monarchBlockDepth = 0;

				string monarchTempName = "";
				string dynastyTempName = "";

				// wars
				bool inActiveWar = false;
				var attackers = new List<string>();
				var defenders = new List<string>();

#if DEBUG
				int linesToDraw = 0;
#endif

				while ((line = reader.ReadLine()) != null)
				{
					linesScanned++;

					string tline = line.Trim();
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
								gameData[key].Add(val);
								if (key == "player")
								{
									playerTag = val;

									string countryName = CountriesTags.CountryTagsToNames.GetValueOrDefault(val);
									gameData["displayed_country_name"].Clear();
									gameData["displayed_country_name"].Add(countryName);
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
					if (!inCountriesSection) continue;

					// Player Tag
					if (!inPlayerCountry)
					{
						if (tline.StartsWith(playerTag + "={"))
						{
							string next = reader.ReadLine()?.Trim();
							if (next == "human=yes") { inPlayerCountry = true; playerCountryDepth = 1; }
						}
						continue;
					}

					if (inPlayerCountry)
					{
						// Country Navigation
						int cOpens = tline.Count(f => f == '{');
						int cCloses = tline.Count(f => f == '}');
						playerCountryDepth += (cOpens - cCloses);

						// Monarch
						if (inMonarchBlock)
						{
							HandleMonarchLine(tline, ref monarchBlockDepth, ref monarchTempName, ref dynastyTempName, gameData);
							if (monarchBlockDepth <= 0) inMonarchBlock = false;
						}

						// Monarch & Gov
						if (tline.StartsWith("monarch={") || tline.StartsWith("monarch_heir={") || tline.StartsWith("monarch_consort={"))
						{
							// regency
							if (tline.StartsWith("monarch_consort={"))
							{
								gameData["is_regency"].Clear();
								gameData["is_regency"].Add("true");
							}
							else
							{
								gameData["is_regency"].Clear();
								gameData["is_regency"].Add("false");
							}

							inMonarchBlock = true;
							monarchBlockDepth = 1;
							monarchTempName = ""; dynastyTempName = "";
						}
						else if (tline.StartsWith("government_rank="))
						{
							gameData["government_rank"].Clear();
							gameData["government_rank"].Add(((GovernmentRank)int.Parse(tline.Split('=')[1])).ToString());
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
						attackers.Clear();
						defenders.Clear();
					}
					else if (inActiveWar)
					{
						if (tline.StartsWith("add_attacker=")) attackers.Add(tline.Split('"')[1]);
						else if (tline.StartsWith("add_defender=")) defenders.Add(tline.Split('"')[1]);
						else if (tline == "}")
						{
							inActiveWar = false;
							ProcessWar(attackers, defenders, playerTag, gameData);
						}
					}
				}
			}
		}

		private static void HandleMonarchLine(string tline, ref int depth, ref string name, ref string dynasty, Dictionary<string, List<string>> gameData)
		{
			int opens = tline.Count(f => f == '{');
			int closes = tline.Count(f => f == '}');
			depth += (opens - closes);

			if(depth == 1)
			{
#if DEBUG
				Console.WriteLine($"{depth} : {tline}");
#endif
				if (tline.StartsWith("name=\"")) name = tline.Split('"')[1];
				else if (tline.StartsWith("dynasty=\"")) dynasty = tline.Split('"')[1];
				else if(tline.StartsWith("succeeded=yes"))
				{
					string fullName = string.IsNullOrEmpty(dynasty) ? name : $"{name} {dynasty}";
					gameData["king_name"].Clear();
					gameData["king_name"].Add(fullName);
				}
			}
		}

		private static void ProcessWar(List<string> attackers, List<string> defenders, string playerTag, Dictionary<string, List<string>> gameData)
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

			gameData["at_war_others_count"].Clear();
			gameData["at_war_others_count"].Add((currentTotal + countToAdd).ToString());
		}
	}
}
