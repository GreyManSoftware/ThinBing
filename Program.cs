using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using System.Reflection;
using System.Linq;

namespace ThinBing
{
    class Program
    {
        static string BaseUrl = "http://www.bing.com/";

        static void Main(string[] args)
        {
            RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            int fail = 0;

            if (args.Contains("-h") || args.Contains("-help"))
                PrintHelp();

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
                    string input = GrabData();

                    // Parse that magical wallpaper JSON
                    Bing data = ParseJson(input);

                    // Grab that tasty image
                    string fileName = Path.Combine(Path.GetTempPath(), "ThinBing" + Path.GetExtension(data.images[0].url));
                    GrabWallpaper(BaseUrl + data.images[0].url, fileName);

                    // Now set that bad boy
                    SetWallpaper(fileName);
                    fail = 0;
                }

                // If it fails, we will just wait a min until we try again
                catch
                {
                    System.Threading.Thread.Sleep(60000);
                    fail++;
                }

                // Checks every 4hrs
                System.Threading.Thread.Sleep(14400 * 1000);
            }

        }

        // None of this currently is of any use until I sort the console attach out
        static void PrintHelp()
        {
            Console.WriteLine("ThinBing.exe [-h, -d]");
            Console.WriteLine("-h, -help : This help");
            Console.WriteLine("-d, -del  : Deletes the start up reg key and exits");
            System.Environment.Exit(0);
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

        static bool CheckStartup(RegistryKey reg)
        {
            object thinBing = Registry.GetValue(reg.Name, "ThinBing", null);

            if (thinBing == null)
                return false;
            else if (thinBing.ToString() != '"' + Path.GetFullPath(Assembly.GetExecutingAssembly().Location) + '"')
                return false;
            else
                return true;
        }

        static void AddStartup(RegistryKey reg)
        {
            object thinBing = Registry.GetValue(reg.Name, "ThinBing", null);

            if (thinBing == null)
                reg.SetValue("ThinBing", '"' + Path.GetFullPath(Assembly.GetExecutingAssembly().Location) + '"');
            else if (thinBing != null && thinBing.ToString() != '"' + Path.GetFullPath(Assembly.GetExecutingAssembly().Location) + '"')
            {
                DelRegKey(reg);
                reg.SetValue("ThinBing", '"' + Path.GetFullPath(Assembly.GetExecutingAssembly().Location) + '"');
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