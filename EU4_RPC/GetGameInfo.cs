
using System;

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

        /*enum State
        {
            Outside,
            InCountries,
            InPlayerCountry,
            FoundHuman,
        }*/

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

            //read first lines
            int linesScanned = 0;
            long startPosition = 0;

            using (StreamReader reader = new StreamReader(filePath))
            {
                string line;

                #region main info
                int maxLinesScanned = 500;

                List<string> list = new List<string>
                {
                    "date",
                    "player",
                    "displayed_country_name",
                    "current_age",
                };

                while ((line = reader.ReadLine()) != null && linesScanned <= maxLinesScanned)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (line.Contains("="))
                    {
                        int equalsIndex = line.IndexOf('=');
                        if (equalsIndex + 1 < line.Length)
                        {
                            string key = line.Substring(0, equalsIndex).Trim();
                            string value = line.Substring(equalsIndex + 1).Trim().Trim('"').Replace("_", " ");

                            if (list.Contains(key) && gameData[key].Count == 0)
                            {
                                gameData[key].Add(value);
                            }
                        }
                    }

                    linesScanned++;
                }

				#endregion

				string playerTag = gameData["player"].FirstOrDefault() ?? "";

				#region government rank and king
				reader.BaseStream.Seek(reader.BaseStream.Length / 4, SeekOrigin.Begin);
				reader.DiscardBufferedData();

                bool inPlayerCountry = false;
                string lastMonarchName = "";

				while ((line = reader.ReadLine()) != null)
				{
					linesScanned++;
					string tline = line.Trim();

					if (!inPlayerCountry)
					{
						if (tline.StartsWith(playerTag + "={"))
						{
							string next = reader.ReadLine()?.Trim();
							if (next != null && next == "human=yes") inPlayerCountry = true;
						}
						continue;
					}

					if (tline.StartsWith("government_rank="))
					{
						if (int.TryParse(tline.Split('=')[1], out int rank))
							gameData["government_rank"].Add(((GovernmentRank)rank).ToString());
					}
					else if (tline.StartsWith("name=\""))
					{
						lastMonarchName = tline.Split('"')[1];
					}
					else if (tline == "flags={") // Safe exit
					{
						break;
					}
				}

				if (!string.IsNullOrEmpty(lastMonarchName))
					gameData["king_name"].Add(lastMonarchName);

				#endregion

				#region active wars
				startPosition = reader.BaseStream.Length * 3 / 4;
                reader.BaseStream.Seek(startPosition, SeekOrigin.Begin);
                reader.DiscardBufferedData();

                bool inActiveWar = false;

				var attackers = new List<string>();
				var defenders = new List<string>();


                while ((line = reader.ReadLine()) != null)
                {
					linesScanned++;
                    string tline = line.Trim();

					if (line.StartsWith("previous_war")) 
                        break;

					if (line.StartsWith("active_war={"))
					{
						inActiveWar = true;
                        attackers.Clear(); 
                        defenders.Clear(); 
                        continue;
					}

					if (inActiveWar)
					{
						if (tline.StartsWith("add_attacker=")) attackers.Add(tline.Split('"')[1]);
						if (tline.StartsWith("add_defender=")) defenders.Add(tline.Split('"')[1]);

						if (attackers.Count > 0 && defenders.Count > 0)
						{
							//inActiveWar = false;
							if (attackers.Contains(playerTag) || defenders.Contains(playerTag))
							{
								string enemy = attackers.Contains(playerTag) ? defenders.FirstOrDefault() : attackers.FirstOrDefault();
								if (enemy != null)
								{
									string enemyName = CountriesTags.CountryTagsToNames.GetValueOrDefault(enemy, enemy);
									if (!gameData["at_war"].Contains(enemyName))
										gameData["at_war"].Add(enemyName);
								}
							}
						}
					}

				}

                #endregion
            }


            #region draw lines + lines count
            Console.WriteLine("Scaned lines: " + linesScanned.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", "."));
            Console.WriteLine();
            foreach (var line in gameData)
            {
                foreach (var i in line.Value)
                    Console.WriteLine(line.Key + ": " + i);
            }
            Console.WriteLine();
            #endregion
            
            return gameData;
        }
    }
}
