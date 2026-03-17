using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection.Metadata;

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
                ["government_rank"] = new(),
                ["at_war"] = new(),
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
				Console.WriteLine("Error: " + ex.Message);
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
				bool inMonarchBlock = false;
				int monarchBlockDepth = 0;

				string monarchTempName = "";
				string dynastyTempName = "";
			
				bool inActiveWar = false;

				var attackers = new List<string>();
				var defenders = new List<string>();

				while ((line = reader.ReadLine()) != null)
				{
					linesScanned++;

					string tline = line.Trim();
					if (tline.Length < 3) continue;

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

					// gov & ruler
					if (tline == "countries={") { inCountriesSection = true; continue; }

					if (inCountriesSection && tline == "provinces={") { inCountriesSection = false; break; }

					if (inCountriesSection && !string.IsNullOrEmpty(playerTag))
					{
						if (!inPlayerCountry && tline.StartsWith(playerTag + "={"))
						{
							string next = reader.ReadLine()?.Trim();
							if (next != null && next == "human=yes") inPlayerCountry = true;
						}
						else if (inPlayerCountry)
						{
							// monarch
							if (tline == "monarch={")
							{
								inMonarchBlock = true;
								monarchBlockDepth = 1;
								monarchTempName = "";
								dynastyTempName = "";
								continue;
							}
							else if (inMonarchBlock)
							{
								if (tline.Contains("{") && !tline.Contains("}")) monarchBlockDepth++;
								if (tline.Contains("}") && !tline.Contains("{")) monarchBlockDepth--;

								if (monarchBlockDepth <= 1)
								{
									inMonarchBlock = false;
								}
								else
								{
									if (monarchBlockDepth == 2)
									{
										if (tline.StartsWith("name=\""))
										{
											monarchTempName = tline.Split('"')[1];
										}
										else if (tline.StartsWith("dynasty=\""))
										{
											dynastyTempName = tline.Split('"')[1];
										}
										else if (tline == "country=\"" + playerTag + "\"")
										{
											string fullName = monarchTempName;
											if (!string.IsNullOrEmpty(dynastyTempName)) fullName += " " + dynastyTempName;

											gameData["king_name"].Clear();
											gameData["king_name"].Add(fullName);
										}
									}
								}
							}
							else if (tline.StartsWith("government_rank="))
							{
								gameData["government_rank"].Add(((GovernmentRank)int.Parse(tline.Split('=')[1])).ToString());
							}
							else if (tline == "}")
							{
								inPlayerCountry = false;
							}
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
		}


		private static void ProcessWar(List<string> attackers, List<string> defenders, string playerTag, Dictionary<string, List<string>> gameData)
		{
			if (attackers.Contains(playerTag) || defenders.Contains(playerTag))
			{
				string enemy = attackers.Contains(playerTag) ? defenders.FirstOrDefault() : attackers.FirstOrDefault();
				if (enemy != null)
				{
					string name = CountriesTags.CountryTagsToNames.GetValueOrDefault(enemy, enemy);
					if (!gameData["at_war"].Contains(name)) gameData["at_war"].Add(name);
				}
			}
		}
	}
}
