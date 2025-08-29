using System.Diagnostics;

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

        static void Main(string[] args)
        {
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

                rpc.UpdateDiscordPresence(saveGameDict);
                while (true)
                {
                    /*if (Process.GetProcessesByName("eu4").Length == 0)
                        break;*/

                    rpc.discord.RunCallbacks();
                    Thread.Sleep(3000);
                }
            }
            catch (Exception e) {
                Console.WriteLine("Main: " + e);
                Console.ReadKey();
            }
        }

        private static void OnSaveChanged(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"File {e.Name} changed. Updating...");
            int attempts = 0;
            const int maxAttempts = 50;
            const int delayMs = 2000;

            while (attempts < maxAttempts)
            {
                try
                {
                    fileWatcher.EnableRaisingEvents = false;
                    rpc.UpdateDiscordPresence(GetGameInfo.ReadSaveGame(autosaveFilePath));
                    break;
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"{attempts}: Error File Access: {ex.Message}. Try {attempts + 1}/{maxAttempts}. Waiting...");
                    attempts++;
                    Thread.Sleep(delayMs);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Something went wrong: {ex.Message}");
                    break;
                }
                finally
                {
                    fileWatcher.EnableRaisingEvents = true;
                }
            }
        }

    }
}
