using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

namespace ThinBing
{
    class Program
    {
        static string BaseUrl = "http://www.bing.com/";

        static void Main(string[] args)
        {
            // Grab Bing JSON
            string input = GrabData();

            // Parse that magical wallpaper JSON
            Bing data = ParseJson(input);

            // Grab that tasty image
            string fileName = Path.Combine(Path.GetTempPath(), "ThinBing" + Path.GetExtension(data.images[0].url));
            GrabWallpaper(BaseUrl + data.images[0].url, fileName);

            // Now set that bad boy
            SetWallpaper(fileName);

        }

        static string GrabData()
        {
            WebClient web = new WebClient();
            return web.DownloadString("https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=1&mkt=en-US");
        }

        static Bing ParseJson(string url)
        {
            JavaScriptSerializer js = new JavaScriptSerializer();
            return js.Deserialize<Bing>(url);
        }

        static void GrabWallpaper(string url, string fileName)
        {
            WebClient web = new WebClient();
            Console.WriteLine("Saving image to: {0}", fileName);
            web.DownloadFile(url, fileName);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern Int32 SystemParametersInfo(UInt32 action, UInt32 uParam, String vParam, UInt32 winIni);

        private static readonly UInt32 SPI_SETDESKWALLPAPER = 0x14;
        private static readonly UInt32 SPIF_UPDATEINIFILE = 0x01;
        private static readonly UInt32 SPIF_SENDWININICHANGE = 0x02;

        static void SetWallpaper(String path)
        {
            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
        }
    }
}