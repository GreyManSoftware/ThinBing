using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

namespace ThinBing
{
	class Program
	{
		internal static class NativeMethods
		{
			// Allows us to print to the console
			[DllImport("kernel32.dll")]
			internal static extern bool AttachConsole(int dwProcessId);
			internal const int ATTACH_PARENT_PROCESS = -1;

			[DllImport("kernel32.dll")]
			internal static extern bool FreeConsole();

			[DllImport("user32.dll", CharSet = CharSet.Auto)]
			internal static extern Int32 SystemParametersInfo(UInt32 action, UInt32 uParam, String vParam, UInt32 winIni);

			internal static readonly UInt32 SPI_SETDESKWALLPAPER = 0x14;
			internal static readonly UInt32 SPIF_UPDATEINIFILE = 0x01;
			internal static readonly UInt32 SPIF_SENDWININICHANGE = 0x02;
		}

		static string BaseUrl = "http://www.bing.com/";
		static string path = '"' + Path.GetFullPath(Assembly.GetExecutingAssembly().Location) + '"';

		static void Main(string[] args)
		{
			// This allows users to get output if they run ThinBing from the cmdline
			NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
			Console.WriteLine();

			RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
			int fail = 0;

			if (args.Contains("-h") || args.Contains("-help"))
				PrintHelp();

			if (CheckForUpdates())
			{
				DelRegKey(rk);
				// Wait to be killed - hopefully this will work
				System.Threading.Thread.Sleep(3600000);
				Environment.Exit(0);
			}
			else
				CheckForOtherProcess();

			if (args.Contains("-d"))
			{
				DelRegKey(rk);
				Console.WriteLine("Deleting reg key and exiting...");
				Environment.Exit(0);
			}

			if (!CheckStartup(rk))
					AddStartup(rk);

			// Run this magic forever and ever
			while (true)
			{
				try
				{
					if (fail > 2)
					{
						Console.WriteLine("Failed too many times. Exiting...");
						Environment.Exit(-1);
					}

					// Grab Bing JSON
					string input = GrabData("https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=1&mkt=en-US");

					if (String.IsNullOrEmpty(input))
					{
						System.Threading.Thread.Sleep(1800 * 1000);
						continue;
					}

					// Parse that magical wallpaper JSON
					Bing data = ParseJson(input);

					// Grab that tasty image
					string fileName = Path.Combine(Path.GetTempPath(), "ThinBing" + Path.GetExtension(data.images[0].url));
					DownloadFile(BaseUrl + data.images[0].url, fileName);

					// Now set that bad boy
					SetWallpaper(fileName);
					fail = 0;
				}

				// If it fails, we will just wait a min until we try again
				catch
				{
					System.Threading.Thread.Sleep(60 * 1000);
					fail++;
				}

				// Checks every 4hrs
				System.Threading.Thread.Sleep(14400 * 1000);

				// Horrible code reuse
				if (CheckForUpdates())
				{
					DelRegKey(rk);
					// Wait to be killed - hopefully this will work
					System.Threading.Thread.Sleep(3600 * 1000);
					Environment.Exit(0);
				}
				else
					CheckForOtherProcess();
			}
		}

		static bool CheckForUpdates()
		{
			// Grab this bad boys version
			Version version = Assembly.GetExecutingAssembly().GetName().Version;
			string output = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ThinBing");
			
			// Grab GitHub Releases
			string json = GrabData("https://api.github.com/repos/CyberChr1s/ThinBing/releases");

			if (String.IsNullOrEmpty(json))
				return false;

			JavaScriptSerializer js = new JavaScriptSerializer();
			
			foreach (var result in js.Deserialize<List<GitHubReleases>>(json))
			{
				if (version.CompareTo(new Version(result.tag_name)) < 0)
				{
					Console.WriteLine("Downloading update");
					if (DownloadFile(result.assets[0].browser_download_url, output + "_" + result.tag_name + ".exe"))
					{
						Process.Start(output + "_" + result.tag_name + ".exe");
						return true;
					}
				}
			}
			return false;
		}

		static void CheckForOtherProcess()
		{
			foreach (Process proc in Process.GetProcesses().Where(p => p.ProcessName.StartsWith("ThinBing") && !p.ProcessName.Contains("vshost")))
			{
				if (proc.Id != Process.GetCurrentProcess().Id)
				{
					int fail = 0;
					string oldBin = null;
					while (fail < 2)
					{
						try
						{
							oldBin = proc.MainModule.FileName;
							Console.WriteLine("Killing old process: {0}", proc.Id);
							proc.Kill();
						}
						catch
						{
							fail++;
							System.Threading.Thread.Sleep(1000);
						}
					}

					// Make sure we don't try to delete ourself!
					if (String.IsNullOrEmpty(oldBin) || oldBin == Path.GetFullPath(Assembly.GetExecutingAssembly().Location))
						continue;

					fail = 0;
					while (fail < 2)
					{
						try
						{
							Console.WriteLine("Cleaning up old bin {0}", oldBin);
							File.Delete(oldBin);
							break;
						}
						catch
						{
							fail++;
							System.Threading.Thread.Sleep(1000);
						}
					}
				}
			}
		}

		static void PrintHelp()
		{
			Console.WriteLine();
			Console.WriteLine("ThinBing.exe [-h, -d]");
			Console.WriteLine("-h, -help : This help");
			Console.WriteLine("-d, -del  : Deletes the start up reg key and exits");
			Environment.Exit(0);
		}

		static string GrabData(string path)
		{
			string response;

			try
			{
				WebClient web = new WebClient();
				web.Headers["User-Agent"] = @"Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/49.0.2623.87 Safari/537.36";
				response = web.DownloadString(path);
			}
			catch
			{
				return null;
			}

			return response;
		}

		static Bing ParseJson(string json)
		{
			JavaScriptSerializer js = new JavaScriptSerializer();
			return js.Deserialize<Bing>(json);
		}

		static bool DownloadFile(string url, string fileName)
		{
			WebClient web = new WebClient();
			Console.WriteLine("Saving image to: {0}", fileName);

			try
			{
				web.DownloadFile(url, fileName);
			}
			catch
			{
				return false;
			}

			return true;
		}

		static void SetWallpaper(String path)
		{
			NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETDESKWALLPAPER, 0, path, NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDWININICHANGE);
		}

		static bool CheckStartup(RegistryKey reg)
		{
			object thinBing = Registry.GetValue(reg.Name, "ThinBing", null);

			if (thinBing == null)
				return false;
			else if (thinBing.ToString() != path)
				return false;
			else
				return true;
		}

		static void AddStartup(RegistryKey reg)
		{
			object thinBing = Registry.GetValue(reg.Name, "ThinBing", null);
			
			if (thinBing == null)
			{
				Console.WriteLine("Adding start up registry key");
				reg.SetValue("ThinBing", path);
			}
			else if (thinBing != null && thinBing.ToString() != path)
			{
				DelRegKey(reg);
				Console.WriteLine("Chaing start up registry key");
				reg.SetValue("ThinBing", path);
			}
		}

		static void DelRegKey(RegistryKey reg)
		{
			Console.WriteLine("Deleting registry key");
			if (CheckStartup(reg))
				reg.DeleteValue("ThinBing");
		}
	}
}