using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Xml;
using System.IO;

using Nekoxy;
using System.Reactive.Linq;

using Grabacr07.KanColleViewer.Composition;
using Grabacr07.KanColleWrapper.Models.Raw;
using Grabacr07.KanColleWrapper;
using KanColleSettings = Grabacr07.KanColleViewer.Models.Settings.KanColleSettings;

using ZTranslation.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ZTranslation
{
	[Export(typeof(IPlugin))]
	[ExportMetadata("Guid", "1E32327A-5D83-4722-9859-3366D86C3FFF")]
	[ExportMetadata("Title", "Z-Translation")]
	[ExportMetadata("Description", "Z-Translation for KanColleViewer")]
	[ExportMetadata("Version", "0.0.1")]
	[ExportMetadata("Author", "WolfgangKurz")] // wolfgangkurzdev@gmail.com
	[ExportMetadata("AuthorURL", "http://swaytwig.com/")]
	public class Plugin : IPlugin
	{
		internal static int ProxyPort => 40729;

		public void Initialize()
		{
			var r = new Random();

			KanColleClient.Current.Proxy.api_start2.TryParse<kcsapi_start2>().Subscribe(s =>
			{
				
			});

			bool started = false;
			KanColleClient.Current.Proxy.SessionSource.Where(x => !started).Subscribe(x =>
				{
					started = true;
					HttpProxy.UpstreamProxyConfig = new ProxyConfig(ProxyConfigType.SpecificProxy, "localhost", ProxyPort);
					
				});

			ModifyProxy.BeforeResponse = (session, data) =>
			{
				if (session.Request.PathAndQuery.StartsWith("/kcs/resources/swf/font.swf"))
				{
					var origin_hash = new byte[] { 0xca, 0xf9, 0xa2, 0x30, 0xfe, 0x46, 0x8a, 0xfc, 0x3d, 0x23, 0x5b, 0xb8, 0xd2, 0xd8, 0x7e, 0x89 };
					var origin_length = 2232291;

					if (data.Length != origin_length) return data;

					byte[] hash;
					using (MD5 md5 = MD5.Create()) hash = md5.ComputeHash(data);
					if (!hash.SequenceEqual(origin_hash)) return data;

					return File.ReadAllBytes(
						Path.Combine(
							Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
							"font.swf"
						)
					);
				}
				else if (session.Request.PathAndQuery != "/kcsapi/api_start2") return data;

				try
				{
					var raw_content = Encoding.UTF8.GetString(data).Substring("svdata=".Length);
					dynamic svdata = JObject.Parse(raw_content);

					{
						// Ship names
						foreach (var x in svdata.api_data.api_mst_ship)
							x.api_name = ShipTranslator.GetTranslation(x.api_name.ToString());

						// Slotitem names
						foreach (var x in svdata.api_data.api_mst_slotitem)
							x.api_name = EquipmentTranslator.GetTranslation(x.api_name.ToString());
					}

					var opt = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii };
					raw_content = JsonConvert.SerializeObject(svdata, opt);
					data = Encoding.UTF8.GetBytes("svdata=" + raw_content);
				}
				catch { }

				return data; // Encoding.UTF8.GetBytes(sv_data);
			};
			KanColleSettings.EnableTranslations.Value = false;
			KanColleClient.Current.Translations.EnableTranslations = false;

			var Server = new SafeTcpServer(ProxyPort, false);
			Server.Start(ModifyProxy.CreateProxy);

			Server.InitListenFinished.WaitOne();
			if (Server.InitListenException != null)
				throw Server.InitListenException;

			Grabacr07.KanColleViewer.Application.Current.Exit += (s, e) => Server.Shutdown();
		}

		private static string TranslationsDir => Path.Combine(
			Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location),
			"Translations"
		);

		private class ShipTranslator
		{
			private static Dictionary<string, string> table;

			static ShipTranslator()
			{
				table = new Dictionary<string, string>();

				XmlDocument doc = new XmlDocument();
				doc.Load(Path.Combine(TranslationsDir, "Ships.xml"));

				var nodes = doc.SelectNodes("/Ships/Ship");
				foreach(XmlNode node in nodes)
				{
					var jp = node["JP-Name"].InnerText;
					var tr = node["TR-Name"].InnerText;

					if (table.ContainsKey(jp)) continue;
					table.Add(jp, tr);
				}
			}
			public static string GetTranslation(string Name) => table.ContainsKey(Name) ? table[Name] : Name;
		}
		private class EquipmentTranslator
		{
			private static Dictionary<string, string> table;

			static EquipmentTranslator()
			{
				table = new Dictionary<string, string>();

				XmlDocument doc = new XmlDocument();
				doc.Load(Path.Combine(TranslationsDir, "Equipment.xml"));

				var nodes = doc.SelectNodes("/Equipment/Item");
				foreach (XmlNode node in nodes)
				{
					var jp = node["JP-Name"].InnerText;
					var tr = node["TR-Name"].InnerText;

					if (table.ContainsKey(jp)) continue;
					table.Add(jp, tr);
				}
			}
			public static string GetTranslation(string Name) => table.ContainsKey(Name) ? table[Name] : Name;
		}
	}
}
