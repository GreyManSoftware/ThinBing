using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ThinBing.Utils
{
	public static class Startup
	{
		public static bool CheckStartup(this RegistryKey reg)
		{
			object thinBing = Registry.GetValue(reg.Name, "ThinBing", null);

			if (thinBing == null)
				return false;
			else if (thinBing.ToString() != Program.exePath)
				return false;
			else
				return true;
		}
		public static void AddStartup(this RegistryKey reg)
		{
			object thinBing = Registry.GetValue(reg.Name, "ThinBing", null);

			if (thinBing == null)
			{
				Program.log.WriteLine(true, "Adding start up registry key");
				reg.SetValue("ThinBing", Program.exePath);
			}
			else if (thinBing != null && thinBing.ToString() != Program.exePath)
			{
				DeleteStartup(reg);
				Program.log.WriteLine(true, "Changing start up registry key");
				reg.SetValue("ThinBing", Program.exePath);
			}
		}

		public static void DeleteStartup(this RegistryKey reg)
		{
			Program.log.WriteLine(true, "Deleting registry key");
			if (CheckStartup(reg))
				reg.DeleteValue("ThinBing");
		}
	}
}
