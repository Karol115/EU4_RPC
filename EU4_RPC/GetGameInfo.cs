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

        private static List<string> list = new List<string>
        {
            "date",
            "player",
            "displayed_country_name",
            "localization", //userless
            "localization", //king name
            "current_age",
        };

        public static Dictionary<string, List<string>> ReadSaveGame(string filePath)
        {
            var gameData = new Dictionary<string, List<string>>();

            //read first lines
            int linesScanned = 0;
            int maxLinesScanned = 500;
            long startPosition = 0;
            int braceCount = 0;

            using (StreamReader reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null && linesScanned <= maxLinesScanned)
                {
                    line = line.Trim();

                    if (line.Contains("="))
                    {
                        int equalsIndex = line.IndexOf('=');

                        //check if value is empty
                        if (equalsIndex + 1 < line.Length)
                        {
                            string key = line.Substring(0, equalsIndex).Trim();
                            string value = line.Substring(equalsIndex + 1).Trim().Trim('"').Replace("_", " ");

                            if (list.Contains(key) && !(gameData.ContainsKey(key) && gameData[key].Count > 1))
                            {
                                if (gameData.ContainsKey(key))
                                {
                                    gameData[key].Add(value);
                                }
                                else
                                {
                                    // Add as list
                                    gameData.Add(key, new List<string> { value });
                                }

                                if (list.Count <= gameData.Count)
                                    break;
                            }
                        }
                    }
                    linesScanned++;
                }


                string playerTag = gameData["player"][0];


                //                                          read government rank
                //startPosition = reader.BaseStream.Length * 1 / 4;
                //reader.BaseStream.Seek(startPosition, SeekOrigin.Begin);

                bool inCountriesBlock = false;
                bool inCountryBlock = false;
                bool[] inCorrectSection = {false, false};
                braceCount = 0;


                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();

                    if (line.StartsWith("countries={"))
                    {
                        inCountriesBlock = true;
                        braceCount = 1;
                        continue;
                    }

                    if (inCountriesBlock && line.StartsWith(playerTag + "={"))
                    {
                        inCountryBlock = true;
                        braceCount = 1;
                        continue;
                    }

                    if (inCountryBlock)
                    {
                        if (line.StartsWith("human=yes"))
                        {
                            inCorrectSection[0] = true;
                            braceCount = 1;
                            continue;
                        }

                        if (inCorrectSection[0] && line.StartsWith("was_player=yes"))
                        {
                            inCorrectSection[1] = true;
                            braceCount = 1;
                            continue;
                        }

                        if (inCorrectSection[0] && inCorrectSection[1])
                        {
                            braceCount += line.Count(c => c == '{');
                            braceCount -= line.Count(c => c == '}');

                            if (line.StartsWith("government_rank="))
                            {
                                int.TryParse(line.Split('=')[1], out int rankNum);
                                string rank = ((GovernmentRank)rankNum).ToString();
                                gameData.Add("government_rank", new List<string> { rank });
                                break;
                            }
                        }
                    }
                    linesScanned++;
                }
            

# Region "This is the code to be collapsed"
                //                                          read last lines(active wars)
                startPosition = reader.BaseStream.Length * 3 / 4;
                reader.BaseStream.Seek(startPosition, SeekOrigin.Begin);
                braceCount = 0;

                bool inActiveWar = false;
                bool foundPlayer = false;

                while ((line = reader.ReadLine()) != null)
                {
                    //Console.WriteLine(reader.BaseStream.Position);
                    line = line.Trim();

                    if (line.StartsWith("active_war={"))
                    {
                        inActiveWar = true;
                        braceCount = 1;
                        continue;
                    }

                    if (inActiveWar)
                    {
                        braceCount += line.Count(c => c == '{');
                        braceCount -= line.Count(c => c == '}');


                        
                        if (line.Contains($"add_attacker=\"{playerTag}\"") || line.Contains($"add_defender=\"{playerTag}\""))
                        {
                            foundPlayer = true;
                        }

                        if (foundPlayer && line.Contains("add_defender="))
                        {
                            string defender = line.Split('"')[1];
                            Console.WriteLine("First defender: " + defender);
                            gameData.Add("at_war", new List<string> { CountriesTags.CountryTagsToNames[defender] });
                            break;
                        }

                        if (braceCount == 0)
                            inActiveWar = false;
                    }
                    linesScanned++;
                }
            }


            Console.WriteLine("Scaned lines: " + linesScanned.ToString());
            Console.WriteLine();
            foreach (var item in gameData)
            {
                foreach (var i in item.Value)
                {
                    Console.WriteLine(item.Key + ": " + i);
                }
            }
            Console.WriteLine();

            return gameData;
        }
    }
}
