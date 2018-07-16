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
using ThinBing.Utils;

namespace ThinBing
{
	class Program
	{
		private static int[] timeSchedule = new int[] { 8, 9, 11, 17 };
		private static int TimeToWait;
		public static Log log = new Log(Path.Combine(Path.GetTempPath() + "ThinBing.log"));
        private static int SleepTime = 30;
		internal static RegistryKey RegKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

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

		public static string BaseUrl = "http://www.bing.com/";
		public static string exePath = '"' + Path.GetFullPath(Assembly.GetExecutingAssembly().Location) + '"';
		static int PropertyTagImageDescription = 0x010E;
		static int PropertyTagCopyright = 0x8298;
		static Regex CopyrightRegex = new Regex(@"([^\(\)]+)", RegexOptions.Compiled);
		static int fail = 0;

		static void Main(string[] args)
		{
			// This allows users to get output if they run ThinBing from the cmdline
			NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
			log.WriteLine();
			
			if (args.Contains("-h") || args.Contains("-help"))
				PrintHelp();

			try
			{
				Updates.CheckForUpdates();
			}
			catch (Exception e)
			{
				Console.WriteLine("Checking for updates failed! - {0}", e);
				throw;
			}

			if (args.Contains("-d"))
			{
				RegKey.DeleteStartup();
				log.WriteLine(true, "Deleting reg key and exiting...");
				Environment.Exit(0);
			}

			if (!RegKey.CheckStartup())
				RegKey.AddStartup();

			// Try and handle going to sleepybyes
			SystemEvents.PowerModeChanged += OnPowerChange;

			Run();
		}

		private static void Run()
		{
			while (true)
			{
				try
				{
					if (fail > 4)
					{
						// This could just be an even longer sleep
						log.WriteLine(true, "Failed too many times. Exiting...");
						Environment.Exit(-1);
					}

					// Grab Bing JSON
					string input = Web.GrabData("https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=1&mkt=en-US");

					if (String.IsNullOrEmpty(input))
					{
						System.Threading.Thread.Sleep(1800 * 1000);
						continue;
					}

					// Parse that magical wallpaper JSON
					Bing data = Web.ParseJson<Bing>(input);

					// Grab that tasty image
					string fileName = Path.Combine(Path.GetTempPath(), "ThinBing" + Path.GetExtension(data.images[0].url));
					byte[] imageData = Web.DownloadData(BaseUrl + data.images[0].url);

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
				TimeToWait = Math.Abs((int)timeDelta.TotalSeconds);
				log.WriteLine(true, "Waiting {0} seconds", TimeToWait);

				// Try to get as close to the times[] as possible
				while (TimeToWait > 0)
				{
					System.Threading.Thread.Sleep(SleepTime * 1000);
					TimeToWait -= SleepTime;
				}

				Updates.CheckForUpdates();
			}
		}

		static void OnPowerChange(object s, PowerModeChangedEventArgs e)
		{
			switch (e.Mode)
			{
				case PowerModes.Resume:
					log.WriteLine(true, "Powered up");
					TimeToWait = -1;
					break;
				case PowerModes.Suspend:
					log.WriteLine(true, "Going to sleep");
                    TimeToWait = -1;
                    break;
				default:
					break;
			}
		}

		static void SetProperty(ref PropertyItem prop, int iId, string sTxt)
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

		static TimeSpan FindNextRunHour()
		{
			DateTime baseDate = DateTime.Now;
			//int curHour = baseDate.GetCurHour12();
			int curHour = baseDate.Hour;
			int nearestHour = timeSchedule.First();
            bool timerSet = false;

			foreach (int time in timeSchedule)
			{
				if (curHour < time)
				{
					nearestHour = time;
                    timerSet = true;
					break;
				}
			}

			DateTime dateNow = new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, curHour, baseDate.Minute, baseDate.Second);
            DateTime compDate;

            if (timerSet)
                compDate = new DateTime(dateNow.Year, dateNow.Month, dateNow.Day, nearestHour, 0, 0);
            else
                compDate = new DateTime(dateNow.Year, dateNow.Month, dateNow.Day, nearestHour, 0, 0).AddDays(1); ;

            return compDate - dateNow;

		}

		static bool ParseImage(string fileName, byte[] imageData, Bing data)
		{
			string directoryName = Path.GetDirectoryName(fileName);
			string extension = Path.GetExtension(fileName);

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

						if (!String.IsNullOrEmpty(description) && !String.IsNullOrEmpty(author))
						{
							// Clean up the strings
							if (description.EndsWith(" "))
								description = description.Substring(0, description.Length - 1);

							// \u00A9 = ©
							if (author.Contains('\u00A9'))
							{
								int chopLocation = author.IndexOf('\u00A9');
								author = author.Substring(chopLocation, author.Length - chopLocation);
							}

							log.WriteLine(true, "Image details: {0} - {1}", description, author);

							SetProperty(ref bingImageProp, PropertyTagImageDescription, description);
							bingImage.SetPropertyItem(bingImageProp);

							SetProperty(ref bingImageProp, PropertyTagCopyright, author);
							bingImage.SetPropertyItem(bingImageProp);
						}

						//TODO: Move this to the end and check if the old file exists
						if (File.Exists(fileName))
						{
							string oldFilename = Path.GetFileNameWithoutExtension(fileName);
							oldFilename += "_" + (int)DateTime.Now.DayOfWeek;
							File.Move(fileName, Path.Combine(directoryName, oldFilename + extension));
						}
					}

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

		// This entire function makes me sick
		public static void CheckForOtherProcess()
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
							log.WriteLine(true, "Killing old process: {0}", proc.Id);
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
							log.WriteLine(true, "Cleaning up old bin {0}", oldBin);
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
			log.WriteLine();
			log.WriteLine(true, "ThinBing.exe [-h, -d]");
			log.WriteLine(true, "-h, -help : This help");
			log.WriteLine(true, "-d, -del  : Deletes the start up reg key and exits");
			Environment.Exit(0);
		}

		static void SetWallpaper(String path)
		{
			NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETDESKWALLPAPER, 0, path, NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDWININICHANGE);
		}
	}
}