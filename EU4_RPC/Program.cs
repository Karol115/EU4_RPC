using AboutConsoleDLL;
using System.Diagnostics;

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

		private static bool isReady = false;
		private static System.Threading.Timer debounceTimer;
		private static int attempts = 0;
		const int maxAttempts = 20;
		const int delayMs = 2000;

		static void Main(string[] args)
		{
			About.ShowAbout(System.Reflection.Assembly.GetExecutingAssembly());

			System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

			#if DEBUG
				isReady = true;
			#endif

			try
			{
				if (args.Length >= 2 && args[0] == "--launch-game")
				{
					string gamePath = args[1];
					if (File.Exists(gamePath))
					{
						Process.Start(new ProcessStartInfo(gamePath) { UseShellExecute = true });
					}
				}

				Setup.CheckForSetup(args);


				if (!Directory.Exists(saveGamePath))
					Directory.CreateDirectory(saveGamePath);

				var directory = new DirectoryInfo(saveGamePath);
				saveFilePath = directory.GetFiles("*.eu4")
					.OrderByDescending(f => f.LastWriteTime)
					.FirstOrDefault()?.FullName;

				if (string.IsNullOrEmpty(saveFilePath) || !File.Exists(saveFilePath))
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

				TriggerUpdate();
			}
			catch (Exception e)
			{
				Console.ForegroundColor = ConsoleColor.DarkRed;
				Console.WriteLine("Main: " + e);
				Console.ResetColor();
			}


#if !DEBUG
			DateTime startTime = DateTime.Now;
			int gracePeriodSeconds = 30;
#endif

			while (true)
			{
#if !DEBUG
				var eu4Processes = Process.GetProcessesByName("eu4");
				if (eu4Processes.Length == 0)
				{
					if (isReady) 
					{
						isReady = false;
						gracePeriodSeconds = 5;
						startTime = DateTime.Now;
						Console.WriteLine("\nEU4 closed. Waiting 5s to exit");
					}

					double elapsed = (DateTime.Now - startTime).TotalSeconds;

					if (elapsed > gracePeriodSeconds)
					{
						Console.ForegroundColor = ConsoleColor.DarkBlue;
						Console.WriteLine("\nEU4 is not running. Closing RPC...");
						Console.ResetColor();

						Thread.Sleep(1000);
						Environment.Exit(0);
					}
					else
					{
						Console.Write($"\rWaiting for EU4 to start RPC... ({gracePeriodSeconds - (int)elapsed}s left)    ");
						Thread.Sleep(1000);
						continue;
					}
				} else {
					if (!isReady) 
					{
						Console.WriteLine();
						Console.ForegroundColor = ConsoleColor.Green;
						Console.WriteLine("EU4 detected! RPC sync is now active.");
						Console.ResetColor();
						isReady = true;
						gracePeriodSeconds = 30;
					}
				}
#endif

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
					if (rpc != null && rpc.discord != null)
					{
						TriggerUpdate();
					}
					Thread.Sleep(10000);
				}
			}
		}

		private static void OnSaveChanged(object sender, FileSystemEventArgs e)
		{
			TriggerUpdate();
		}

		private static void TriggerUpdate(FileInfo? file = null)
		{
			if (!isReady) return;

			if (file == null)
			{
				var directory = new DirectoryInfo(saveGamePath);
				if (!directory.Exists) return;

				file = directory.EnumerateFiles("*.eu4")
					.Where(f => f.Length > 0)
					.OrderByDescending(f => f.LastWriteTime)
					.FirstOrDefault();
			}

			if (file == null) return;

			saveFilePath = file.FullName;
			attempts = 0;

			debounceTimer ??= new System.Threading.Timer(_ =>
			{
				if (!File.Exists(saveFilePath)) return;

				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.Write("The file ");
				Console.ForegroundColor = ConsoleColor.DarkYellow;
				Console.Write($"{Path.GetFileName(saveFilePath)}");
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine(" was last changed. Updating...");

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
