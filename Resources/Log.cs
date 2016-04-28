using System;
using System.IO;

namespace ThinBing.Resources
{
	class Log
	{
		private string outputFilePath;

		public Log(string path)
		{
			outputFilePath = path;
			string[] welcomeMessage = new string[]{ "## ThinBing - Grey Man Software - Chris Davies 2016 ##", "#### thinbing.greymansoftware.com ####" };
			File.WriteAllLines(outputFilePath, welcomeMessage);
		}

		public void WriteLine(bool date, string message, params object[] sections)
		{
			string newMessage = DateTime.Now.ToString() + ": " + message;
			WriteLine(newMessage, sections);
		}

		public void WriteLine(string message, params object[] sections)
		{
			using (StreamWriter file = new StreamWriter(outputFilePath, true))
				file.WriteLine(String.Format(message, sections));

			Console.WriteLine(message, sections);
		}

		public void WriteLine()
		{
			using (StreamWriter file = new StreamWriter(outputFilePath, true))
				file.WriteLine();

			Console.WriteLine();
		}
	}
}
