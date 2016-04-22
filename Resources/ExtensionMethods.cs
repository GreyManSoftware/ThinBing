using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThinBing.Resources
{
	static class ExtensionMethods
	{
		public static int GetCurHour12 (this DateTime date)
		{
			date = DateTime.Now;
			if (date.Hour > 12)
				return ((date.Hour + 11) % 12) + 1;

			return date.Hour;
		}
	}
}
