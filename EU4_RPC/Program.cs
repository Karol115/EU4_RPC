using AboutConsoleDLL;

namespace EU4_RPC
{
    internal class Program
    {
		public static string saveGamePath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
			"Paradox Interactive",
			"Europa Universalis IV",
			"Save games"
		);

        private static string? saveFilePath;
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

			System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

			try
            {
				if (!Directory.Exists(saveGamePath))
					Directory.CreateDirectory(saveGamePath);

				var directory = new DirectoryInfo(saveGamePath);
				saveFilePath = directory.GetFiles("*.eu4")
					.OrderByDescending(f => f.LastWriteTime)
					.FirstOrDefault()?.FullName;

				if (!string.IsNullOrEmpty(saveFilePath) && File.Exists(saveFilePath))
                {
                    saveGameDict = GetGameInfo.ReadSaveGame(saveFilePath);
                }
                else
                {
					Console.ForegroundColor = ConsoleColor.DarkYellow;
					Console.WriteLine("No save files found. Waiting for game save...");
					Console.ResetColor();
				}

                rpc = new RPC();

                fileWatcher = new FileSystemWatcher(saveGamePath);

                fileWatcher.Filter = "*.eu4";
                fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
                fileWatcher.Changed += OnSaveChanged;
                fileWatcher.Created += OnSaveChanged;
                fileWatcher.Renamed += OnSaveChanged;
                fileWatcher.EnableRaisingEvents = true;
            }
            catch (Exception e)
            {
				Console.ForegroundColor = ConsoleColor.DarkRed;
				Console.WriteLine("Main: " + e);
                Console.ResetColor();
            }

			while (true)
            {
                if (rpc.discord != null)
                {
                    try
                    {
                        rpc.discord.RunCallbacks();
					}
                    catch (Exception)
                    {
						Console.ForegroundColor = ConsoleColor.DarkMagenta;
						Console.WriteLine("Discord client not detected #1. Retrying in 10 seconds...");
						Console.ResetColor();

						rpc.discord.Dispose();
                        rpc.discord = null;
                    }
					Thread.Sleep(1000);
				} 
                else
                {
					rpc.Initialize();
					if (rpc.discord != null)
					{
						if (saveGameDict != null) rpc.UpdateDiscordPresence(saveGameDict);
                        Thread.Sleep(1000);
						continue;
					}
					Thread.Sleep(10000);
				}
            }
        }

        private static void OnSaveChanged(object sender, FileSystemEventArgs e)
        {
			var directory = new DirectoryInfo(saveGamePath);
			var latestFile = directory.EnumerateFiles("*.eu4")
                .Where(f => f.Length > 0)
				.OrderByDescending(f => f.LastWriteTime)
				.FirstOrDefault();

			if (latestFile == null) return;

            saveFilePath = latestFile.FullName;
			attempts = 0;

            debounceTimer ??= new Timer(_ =>
            {
				if (!File.Exists(saveFilePath)) return;

				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine($"File {saveFilePath} changed. Updating...");
				Console.ResetColor();

				try
                {
					Console.ForegroundColor = ConsoleColor.Green;
					Console.WriteLine("Updating Discord presence...");
					Console.ResetColor();

					rpc.UpdateDiscordPresence(GetGameInfo.ReadSaveGame(saveFilePath));
                    debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
                }
                catch (IOException ex)
                {
                    attempts++;

					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("Error reading file: " + ex.Message);
					Console.ResetColor();

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
