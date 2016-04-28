using System;
using System.IO;

namespace ThinBing.Resources
{
	class Log
	{
		private string outputFilePath;
        private int MaxLogSize = 10 * 1024 * 1024;


        public Log(string path)
		{
			outputFilePath = path;
            CreateLogFile();
		}

        public void CreateLogFile()
        {
            string[] welcomeMessage = new string[] { "## ThinBing - Grey Man Software - Chris Davies 2016 ##", "## thinbing.greymansoftware.com ##" };
            File.WriteAllLines(outputFilePath, welcomeMessage);
        }

		public void WriteLine(bool date, string message, params object[] sections)
		{
			string newMessage = DateTime.Now.ToString() + ": " + message;
			WriteLine(newMessage, sections);
		}

		public void WriteLine(string message, params object[] sections)
		{
            //using (StreamWriter file = new StreamWriter(outputFilePath, true))
            //	file.WriteLine(String.Format(message, sections));

            File.AppendAllText(outputFilePath, String.Format(message, sections) + "\r\n");
			Console.WriteLine(message, sections);
		}

		public void WriteLine()
		{
            File.AppendAllText(outputFilePath, "\r\n");
            Console.WriteLine();
		}

        public void TruncateLog()
        {
            if (new FileInfo(outputFilePath).Length >= MaxLogSize)
            {
                File.Delete(outputFilePath);
                CreateLogFile();
                File.AppendAllText(outputFilePath, "# truncated log #");
            }
        }
	}
}
