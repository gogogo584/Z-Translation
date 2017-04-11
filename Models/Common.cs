using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Reflection;

namespace ZTranslation.Models
{
	internal class Common
	{
		public static string UpdateWebpage => "https://github.com/WolfgangKurz/Z-Translation/releases";

		public static string PatchedFilesDirectory => Path.Combine(
			Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
			"Z-Translation"
		);

		// Check Update
		public static string CheckUpdate()
		{
			Stream stream = HTTPRequest.Request("https://raw.githubusercontent.com/WolfgangKurz/Z-Translation/master/version.txt");
			if (stream == null) return "";

			try
			{
				StreamReader reader = new StreamReader(stream);

				Assembly assembly = Assembly.GetExecutingAssembly();
				Version Version = assembly.GetName().Version;

				string ver = reader.ReadToEnd();

				int[] part = ver.Split('.').Select(x => int.Parse(x)).ToArray();
				if (part[0] > Version.Major)
					return ver;
				else if (part[0] == Version.Major && part[1] > Version.Minor)
					return ver;
				else if (part[0] == Version.Major && part[1] == Version.Minor && part[2] > Version.Build)
					return ver;
				else if (part[0] == Version.Major && part[1] == Version.Minor && part[2] == Version.Build && part[3] > Version.Revision)
					return ver;
				else
					return "";
			}
			catch { }

			return "";
		}
	}
}
