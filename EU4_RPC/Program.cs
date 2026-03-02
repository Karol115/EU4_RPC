using AboutConsoleDLL;
using System.Diagnostics.Metrics;
using System.Security.Authentication.ExtendedProtection;

namespace EU4_RPC
{
    internal class Program
    {
        /*public static string DecompressFile(string filePath)
        {
            using (FileStream originalFileStream = File.OpenRead(filePath))
            using (MemoryStream decompressedStream = new MemoryStream())
            {
                using (DeflateStream decompressionStream = new DeflateStream(originalFileStream, CompressionMode.Decompress))
                {
                    decompressionStream.CopyTo(decompressedStream);
                }
                return System.Text.Encoding.UTF8.GetString(decompressedStream.ToArray());
            }
        }*/

        public static string saveGamePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Paradox Interactive/Europa Universalis IV/Save games/");
        private static string autosaveFilePath = Path.Combine(saveGamePath, "autosave.eu4");
        public static Dictionary<string, List<string>> saveGameDict;
        private static FileSystemWatcher fileWatcher;
        private static RPC rpc;

        private static Timer debounceTimer;
        private static int attempts = 0;
        const int maxAttempts = 20;
        const int delayMs = 2000;

        static void Main(string[] args)
        {
            About.ShowAbout(System.Reflection.Assembly.GetExecutingAssembly());

            try
            {
                saveGameDict = GetGameInfo.ReadSaveGame(autosaveFilePath);
                rpc = new RPC();

                fileWatcher = new FileSystemWatcher(saveGamePath);

                fileWatcher.Filter = "autosave.eu4";
                fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
                fileWatcher.Changed += OnSaveChanged;
                fileWatcher.Created += OnSaveChanged;
                fileWatcher.Renamed += OnSaveChanged;
                fileWatcher.EnableRaisingEvents = true;

                /*foreach (var item in saveGameDict)
                {
                    foreach (var i in item.Value)
                    {
                        Console.WriteLine(item.Key + ": " + i);
                    }
                }*/
            }
            catch (Exception e)
            {
                Console.WriteLine("Main: " + e);
            }

			int counter = 0;
			while (true)
            {
                if (rpc.discord != null)
                {
                    try
                    {
                        rpc.discord?.RunCallbacks();
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Discord client not detected #1. Retrying in 10 seconds...");
                        rpc.discord.Dispose();
                        rpc.discord = null;
                    }
                } 
                else
                {
					if (counter % 100 == 0) rpc.Initialize();
				}

                counter++;
                Thread.Sleep(100);
            }
        }

        private static void OnSaveChanged(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"File {e.Name} changed. Updating...");
            attempts = 0;

            debounceTimer ??= new Timer(_ =>
            {
                try
                {
                    Console.WriteLine("Updating Discord presence...");
                    rpc.UpdateDiscordPresence(GetGameInfo.ReadSaveGame(autosaveFilePath));
                    debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
                }
                catch (IOException ex)
                {
                    attempts++;
                    Console.WriteLine("Error reading file: " + ex.Message);
                    if (attempts < maxAttempts)
                    {
                        debounceTimer.Change(delayMs, Timeout.Infinite);
                    }
                    else
                    {
                        debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    }
                }
            }, null, Timeout.Infinite, Timeout.Infinite);

            debounceTimer.Change(500, Timeout.Infinite);
        }

    }
}
