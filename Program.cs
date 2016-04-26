using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using ThinBing.Resources;

namespace ThinBing
{
	class Program
	{
		private static int[] timeSchedule = new int[] { 8, 9, 11, 17 };

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
		static string exePath = '"' + Path.GetFullPath(Assembly.GetExecutingAssembly().Location) + '"';
		static int PropertyTagImageDescription = 0x010E;
		static int PropertyTagCopyright = 0x8298;
		static Regex CopyrightRegex = new Regex(@"([^\(\)]+)", RegexOptions.Compiled);
		static int fail = 0;

		static void Main(string[] args)
		{
			// This allows users to get output if they run ThinBing from the cmdline
			NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
			Console.WriteLine();

			RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
			
			if (args.Contains("-h") || args.Contains("-help"))
				PrintHelp();

			CheckForUpdates(rk);

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
					if (fail > 4)
					{
						// This could just be an even longer sleep
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
					Bing data = ParseJson<Bing>(input);

					// Grab that tasty image
					string fileName = Path.Combine(Path.GetTempPath(), "ThinBing" + Path.GetExtension(data.images[0].url));
					byte[] imageData = DownloadData(BaseUrl + data.images[0].url);

					// Parse that sexy image
					if (!ParseImage(fileName, imageData, data))
						continue;

					// Now set that bad boy
					SetWallpaper(fileName);
					fail = 0;
				}

				// If it fails, wait a min
				catch
				{
					System.Threading.Thread.Sleep(60 * 1000);
					fail++;
				}

				TimeSpan timeDelta = FindNextRunHour();
				int TimeToWait = Math.Abs((int)timeDelta.TotalSeconds);
				Console.WriteLine("Waiting {0} seconds", TimeToWait);

				// Try to get as close to the times[] as possible
				System.Threading.Thread.Sleep(TimeToWait * 1000);
				CheckForUpdates(rk);
			}
		}

		static void SetProperty(ref System.Drawing.Imaging.PropertyItem prop, int iId, string sTxt)
		{
			int iLen = sTxt.Length + 1;
			byte[] bTxt = new Byte[iLen];
			for (int i = 0; i < iLen - 1; i++)
				bTxt[i] = (byte)sTxt[i];
			bTxt[iLen - 1] = 0x00;
			prop.Id = iId;
			prop.Type = 2;
			prop.Value = bTxt;
			prop.Len = iLen;
		}

		static void CheckForUpdates(RegistryKey rk)
		{
			if (CheckForLatestVersion())
			{
				DelRegKey(rk);

				// This sleep allows for the newly download file to end this processes life
				// We don't simply just exit, because the other process gets the file pa
				// from the running process. Tacky, I know :)
				System.Threading.Thread.Sleep(3600 * 1000);

				// We shouldn't ever get here
				Environment.Exit(0);
			}
			else
				CheckForOtherProcess();

		}

		static TimeSpan FindNextRunHour()
		{
			DateTime baseDate = DateTime.Now;
			//int curHour = baseDate.GetCurHour12();
			//int curHour = baseDate.Hour;
			int curHour = 18;
			int nearestHour = timeSchedule.First();

			foreach (int time in timeSchedule)
			{
				if (curHour < time)
				{
					nearestHour = time;
					break;
				}
			}

			var dateNow = new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, curHour, baseDate.Minute, baseDate.Second);
			var compDate = new DateTime(dateNow.Year, dateNow.Month, dateNow.Day, nearestHour, 0, 0);

			return compDate - dateNow;

		}

		static bool ParseImage(string fileName, byte[] imageData, Bing data)
		{
			using (MemoryStream ms = new MemoryStream(imageData))
			{
				System.Drawing.Image bingImage = System.Drawing.Image.FromStream(ms);

				// Incredibly awesome hack to get around the lack of a static construtor for PropertyItem
				PropertyItem bingImageProp = (PropertyItem)FormatterServices.GetUninitializedObject(typeof(PropertyItem));

				// Not sure this try is required anymore, but just in case
				try
				{
					if (data.images[0].copyright != null && CopyrightRegex.IsMatch(data.images[0].copyright))
					{
						MatchCollection match = CopyrightRegex.Matches(data.images[0].copyright);
						string description = match[0].Value;
						string author = match[1].Value;

						SetProperty(ref bingImageProp, PropertyTagImageDescription, description);
						bingImage.SetPropertyItem(bingImageProp);

						SetProperty(ref bingImageProp, PropertyTagCopyright, author);
						bingImage.SetPropertyItem(bingImageProp);

						bingImage.Save(fileName);
					}
					else
						bingImage.Save(fileName);
				}
				catch
				{
					fail++;
					System.Threading.Thread.Sleep(60 * 1000);
					return false;
				}
			}

			return true;
		}

		static bool CheckForLatestVersion()
		{
			// Grab this bad boys version
			Version version = Assembly.GetExecutingAssembly().GetName().Version;
			string output = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ThinBing");
			
			// Grab GitHub Releases
			string json = GrabData("https://api.github.com/repos/GreyManSoftware/ThinBing/releases");

			if (String.IsNullOrEmpty(json))
				return false;

			foreach (var result in ParseJson<List<GitHubReleases>>(json))
			{
				if (version.CompareTo(new Version(result.tag_name)) < 0)
				{
					Console.WriteLine("Downloading update: {0}", result.tag_name);
					if (DownloadData(result.assets[0].browser_download_url, output + "_" + result.tag_name + ".exe"))
					{
						Process.Start(output + "_" + result.tag_name + ".exe");
						return true;
					}
				}
			}
			return false;
		}

		// This entire function makes me sick
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

		static T ParseJson<T>(string json)
		{
			JavaScriptSerializer js = new JavaScriptSerializer();
			return js.Deserialize<T>(json);
		}

		static bool DownloadData(string url, string fileName)
		{
			WebClient web = new WebClient();
			Console.WriteLine("Saving file to: {0}", fileName);

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

		static byte[] DownloadData(string url)
		{
			Console.WriteLine("Downloading data...");
			WebClient web = new WebClient();
			return web.DownloadData(url);
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
			else if (thinBing.ToString() != exePath)
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
				reg.SetValue("ThinBing", exePath);
			}
			else if (thinBing != null && thinBing.ToString() != exePath)
			{
				DelRegKey(reg);
				Console.WriteLine("Changing start up registry key");
				reg.SetValue("ThinBing", exePath);
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