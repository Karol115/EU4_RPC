
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

        enum State
        {
            Outside,
            InCountries,
            InPlayerCountry,
            FoundHuman,
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

					if (line.IndexOf("key=\"longest_reign\"", StringComparison.Ordinal) >= 0)
					{
                        string next = reader.ReadLine()?.Trim();
                        if (next != null && next.StartsWith("localization="))
                        {
                            string kingName = next.Split('=')[1].Trim().Trim('"');
                            gameData["king_name"].Add(kingName);
                        }
                    }

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

                string playerTag = gameData["player"][0];
                
                #region government rank
                State state = State.Outside;
                startPosition = reader.BaseStream.Length * 1 / 4;
                reader.BaseStream.Seek(startPosition, SeekOrigin.Begin);
                reader.DiscardBufferedData();

                while ((line = reader.ReadLine()) != null)
                { 
					string trimmedLine = line.Trim();

				    switch (state)
                    {
                        case State.Outside:
                            if (line.StartsWith("countries={"))
                                state = State.InCountries;
                            break;

                        case State.InCountries:
							if (trimmedLine.StartsWith(playerTag + "={"))
								state = State.InPlayerCountry;
                            break;

                        case State.InPlayerCountry:
							if (trimmedLine.Contains("human=yes"))
								state = State.FoundHuman;

							if (trimmedLine == "}") state = State.InCountries;
							break;

						case State.FoundHuman:
							if (trimmedLine.StartsWith("government_rank="))
							{
								var parts = trimmedLine.Split('=');
								if (parts.Length > 1 && int.TryParse(parts[1], out int rankNum))
                                {
                                    string rank = ((GovernmentRank)rankNum).ToString();
                                    gameData["government_rank"].Add(rank);
                                    goto Done;
                                }
                            }

							if (trimmedLine == "}") state = State.InCountries;
							break;
					}

                    linesScanned++;
                }

                Done:;
                #endregion

                #region active wars
                startPosition = reader.BaseStream.Length * 3 / 4;
                reader.BaseStream.Seek(startPosition, SeekOrigin.Begin);
                reader.DiscardBufferedData();

                bool inActiveWar = false;
                List<string> blockLines = new();

                while ((line = reader.ReadLine()) != null)
                {
                    if(line.Contains("active_war={", StringComparison.Ordinal))
                    {
                        inActiveWar = true;
                        blockLines.Clear();
                        continue;
                    }

                    if (inActiveWar)
                    {
						if (line.Trim().StartsWith("battle={")) // end of at war countries list
						{
                            inActiveWar = false;

                            var attackers = new List<string>();
                            var defenders = new List<string>();

                            foreach (var l in blockLines)
                            {
                                if (l.StartsWith("add_attacker="))
                                {
                                    var parts = l.Split('"');
                                    if(parts.Length > 1) attackers.Add(parts[1]);
                                }
                                else if (l.StartsWith("add_defender="))
                                {
                                    var parts = l.Split('"');
                                    if(parts.Length > 1) defenders.Add(parts[1]);
                                }
                            }

                            if (attackers.Contains(playerTag) || defenders.Contains(playerTag))
                            {
                                string enemyTag = attackers.Contains(playerTag) ? defenders.FirstOrDefault() : attackers.FirstOrDefault();
                                if (enemyTag != null)
                                {
                                    if (!CountriesTags.CountryTagsToNames.TryGetValue(enemyTag, out var enemyName))
                                        enemyName = enemyTag;

                                    gameData["at_war"].Add(enemyName);
                                }
                            }
                        }
                        else
                        {
                            blockLines.Add(line);
                        }

                        if (line.StartsWith("previous_war"))
                            break;
                    }

                    linesScanned++;
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
