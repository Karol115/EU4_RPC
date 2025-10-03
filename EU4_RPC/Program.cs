using AboutConsoleDLL;

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

        static void Main(string[] args)
        {
            /*Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("Copyright \u00A9 2023-2025 Karol115 All rights reserved.");*/
            About.ShowAbout(System.Reflection.Assembly.GetExecutingAssembly());

            try
            {
                saveGameDict = GetGameInfo.ReadSaveGame(autosaveFilePath);
                rpc = new RPC();

                fileWatcher = new FileSystemWatcher(saveGamePath);

                fileWatcher.Filter = "autosave.eu4";
                fileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                fileWatcher.Changed += OnSaveChanged;
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


            while (true)
            {
                try
                {
                    rpc.discord?.RunCallbacks();
                }
                catch (Exception)
                {
                    Console.WriteLine("Discord client not detected #1. Retrying in 10 seconds...");
                    Thread.Sleep(9000);
                    rpc.Initialize();
                }

                Thread.Sleep(1000);
            }
            Console.ReadKey();
        }

        private static void OnSaveChanged(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"File {e.Name} changed. Updating...");
            int attempts = 0;
            const int maxAttempts = 20;
            const int delayMs = 2000;

            debounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);

            debounceTimer = new Timer(_ =>
            {
                attempts++;

                try
                {
                    Console.WriteLine("Updating Discord presence...");
                    rpc.UpdateDiscordPresence(GetGameInfo.ReadSaveGame(autosaveFilePath));
                }
                catch (IOException ex)
                {
                    Console.WriteLine("Error reading file: " + ex.Message);
                    if (attempts < maxAttempts)
                    {
                        //try again
                        debounceTimer?.Change(delayMs, Timeout.Infinite);
                    }
                    else
                    {
                        //end
                        debounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                    }
                }
            }, null, 500, Timeout.Infinite);
        }

    }
}
