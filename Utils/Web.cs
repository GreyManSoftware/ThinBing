using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ThinBing.Utils
{
	public static class Web
	{
		public static string GrabData(string path)
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

		public static T ParseJson<T>(string json)
		{
			JavaScriptSerializer js = new JavaScriptSerializer();
			return js.Deserialize<T>(json);
		}

		public static bool DownloadData(string url, string fileName)
		{
			WebClient web = new WebClient();
			Program.log.WriteLine(true, "Saving file to: {0}", fileName);

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

		public static byte[] DownloadData(string url)
		{
			Program.log.WriteLine(true, "Downloading data...");
			WebClient web = new WebClient();
			return web.DownloadData(url);
		}
	}
}
