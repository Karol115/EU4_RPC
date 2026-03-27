using System.Diagnostics;
using IWshRuntimeLibrary;
using File = System.IO.File;

namespace EU4_RPC
{
	public static class Setup
	{
		public static string batName = "eu4_with_rpc.bat";
		public static string lnkName = "EU4 with Discord RPC";

		public static void CheckForSetup(string[] args)
		{
			string batPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, batName);

			if (args.Length > 0) return;

			if (!File.Exists(batPath)) {
				Console.ForegroundColor = ConsoleColor.Red;
				Console.Write("[MISSING CONFIG] ");
				Console.ResetColor();
			}
			Console.Write("Enter");
			Console.ForegroundColor = ConsoleColor.Magenta;
			Console.Write(" 'c' ");
			Console.ResetColor();
			Console.Write("(or press");
			Console.ForegroundColor = ConsoleColor.Magenta;
			Console.Write(" Enter/wait 3s ");
			Console.ResetColor();
			Console.Write("to skip) to let program create launcher for eu4 and rpc: ");
			Console.ResetColor();

			for (int i = 0; i < 30; i++)
			{
				if (Console.KeyAvailable)
				{
					var keyInfo = Console.ReadKey(intercept: true);
					if (keyInfo.Key == ConsoleKey.C)
					{
						Console.WriteLine("\nStarting Setup...");
						SetupWizard();
						Environment.Exit(0);
						return;
					}
					else if (keyInfo.Key == ConsoleKey.Enter)
					{
						Console.WriteLine();
						return;
					}
				}
				Thread.Sleep(100);
				if (i % 10 == 0 && i > 0) Console.Write(".");
			}
			Console.WriteLine("\nSkipping setup...");

			Console.WriteLine();
		}

		public static void SetupWizard()
		{
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine("=== EU4 RPC: Configurator ===");
			Console.ResetColor();
			Console.WriteLine("\nNow I will prepare .bat and shortcut for you. This bat file will launch your eu4 and rpc at once.");
			Console.WriteLine("Please launch Europa Universalis IV now to start auto configuration.");

			Process? eu4Process = null;
			while (eu4Process == null)
			{
				eu4Process = Process.GetProcessesByName("eu4").FirstOrDefault();
				if (eu4Process == null)
				{
					Thread.Sleep(1000);
					Console.Write(".");
				}
			}

			try
			{
				string exePath = eu4Process.MainModule.FileName;
				string rpcPath = Process.GetCurrentProcess().MainModule.FileName;
				string directory = AppDomain.CurrentDomain.BaseDirectory;

				Console.WriteLine($"\nGame detected: {exePath}");

				string batContent = $"@echo off\nstart \"\" \"{exePath}\"\nstart \"\" \"{rpcPath}\"";
				File.WriteAllText(Path.Combine(directory, batName), batContent);
				Console.WriteLine($"[1/2] File {batName} created.");

				string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
				string shortcutPath = Path.Combine(desktopPath, $"{lnkName}.lnk");

				WshShell shell = new WshShell();
				IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);

				shortcut.TargetPath = rpcPath;
				shortcut.Arguments = $"--launch-game \"{exePath}\"";
				shortcut.Description = "Launch EU4 with Discord Rich Presence By Karol115";

				shortcut.IconLocation = $"{exePath},0";

				shortcut.WorkingDirectory = directory;
				shortcut.Save();

				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine("[2/2] Desktop shortcut created with the original EU4 icon!");
				Console.ResetColor();
				Console.WriteLine($"Setup complete! Now you can use the '{lnkName}' shortcut on your desktop. Enjoy playing");
				Console.WriteLine($"Press any key to exit");

			}
			catch (Exception ex)
			{
				Console.WriteLine($"\n[Error] Setup failed: {ex.Message}");
			}
			
			Console.ReadKey();
		}
	}
}