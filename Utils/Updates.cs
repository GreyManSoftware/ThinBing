using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using ThinBing.Resources;

namespace ThinBing.Utils
{
	public static class Updates
	{
		public static void CheckForUpdates()
		{
			if (CheckForLatestVersion())
			{
				Program.RegKey.DeleteStartup();

				// This sleep allows for the newly download file to end this processes life
				// We don't simply just exit, because the other process gets the file pa
				// from the running process. Tacky, I know :)
				Thread.Sleep(3600 * 1000);

				// We shouldn't ever get here
				Environment.Exit(0);
			}
			else
				Program.CheckForOtherProcess();

		}

		public static bool CheckForLatestVersion()
		{
			// Grab this bad boys version
			Version version = Assembly.GetExecutingAssembly().GetName().Version;
			string output = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ThinBing");

			// Grab GitHub Releases
			string json = Web.GrabData("https://api.github.com/repos/GreyManSoftware/ThinBing/releases");

			if (String.IsNullOrEmpty(json))
				return false;

			foreach (var result in Web.ParseJson<List<GitHubReleases>>(json))
			{
				if (version.CompareTo(new Version(result.tag_name)) < 0)
				{
					Program.log.WriteLine(true, "Downloading update: {0}", result.tag_name);
					if (Web.DownloadData(result.assets[0].browser_download_url, output + "_" + result.tag_name + ".exe"))
					{
						Process.Start(output + "_" + result.tag_name + ".exe");
						return true;
					}
				}
			}
			return false;
		}
	}
}
