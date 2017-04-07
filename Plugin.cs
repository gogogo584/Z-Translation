using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Net;
using System.Xml;
using System.IO;
using System.Threading;

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
			bool started = false;
			KanColleClient.Current.Proxy.SessionSource.Where(x => !started).Subscribe(x =>
				{
					started = true;
					HttpProxy.UpstreamProxyConfig = new ProxyConfig(ProxyConfigType.SpecificProxy, "localhost", ProxyPort);
					
				});

			ModifyProxy.BeforeResponse = (session, data) =>
			{
				#region Font patch and api_start2 watching
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
				#endregion

				try
				{
					Func<string, string, string> getTranslation = (x, y) => Translators[x]?.GetTranslation(y) ?? y;

					var raw_content = Encoding.UTF8.GetString(data).Substring("svdata=".Length);
					dynamic svdata = JObject.Parse(raw_content);

					{
						// Ship names
						foreach (var x in svdata.api_data.api_mst_ship)
						{
							x.api_name = getTranslation("ShipName", x.api_name.ToString());
							x.api_getmes = getTranslation("ShipGetMessage", x.api_getmes.ToString());
						}

						// Slotitem names
						foreach (var x in svdata.api_data.api_mst_slotitem)
						{
							x.api_name = getTranslation("EquipmentName", x.api_name.ToString());
							x.api_info = getTranslation("EquipmentInfo", x.api_info.ToString());
						}
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

			PrepareTranslators();

			var Server = new SafeTcpServer(ProxyPort, false);
			Server.Start(ModifyProxy.CreateProxy);

			Server.InitListenFinished.WaitOne();
			if (Server.InitListenException != null)
				throw Server.InitListenException;

			Grabacr07.KanColleViewer.Application.Current.Exit += (s, e) => Server.Shutdown();
		}

		private ConcurrentDictionary<string, XmlTranslator> Translators { get; set; }
		private void PrepareTranslators()
		{
			Action<string, string> RemoteLoader = (name, url) =>
			{
				var urlBase = "https://raw.githubusercontent.com/WolfgangKurz/Z-Translation/master/Translations/";
				new Thread(() =>
				{
					string xml = "";

						HttpWebRequest rq = WebRequest.Create(urlBase + url) as HttpWebRequest;
						rq.Timeout = 5000;
						HttpWebResponse response = rq.GetResponse() as HttpWebResponse;

						using (var reader = new StreamReader(response.GetResponseStream()))
							xml = reader.ReadToEnd();

						var translator = new XmlTranslator(xml, "/Texts/Text", true);
						Translators.TryAdd(name, translator);
				}).Start();
			};

			Translators = new ConcurrentDictionary<string, XmlTranslator>();
			Translators.TryAdd("ShipName", ShipTranslator.Instance);
			Translators.TryAdd("EquipmentName", EquipmentTranslator.Instance);

			RemoteLoader("ShipGetMessage", "ShipGetMessage.xml");
			RemoteLoader("EquipmentInfo", "EquipmentInfo.xml");
		}

		private static string TranslationsDir => Path.Combine(
			Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location),
			"Translations"
		);
		private class XmlTranslator
		{
			protected Dictionary<string, string> table;

			public XmlTranslator()
			{
				table = new Dictionary<string, string>();
			}
			public XmlTranslator(string xmlPath, string textSelector = "/Texts/Text", bool fromString = false) : this()
			{
				if(fromString)
					this.LoadXml(xmlPath, textSelector);
				else
					this.Load(xmlPath, textSelector);
			}

			public void Load(string xmlPath, string textSelector)
			{
				XmlDocument doc = new XmlDocument();
				doc.Load(xmlPath);

				var nodes = doc.SelectNodes(textSelector);
				foreach(XmlNode node in nodes)
				{
					var jp = node["JP-Name"].InnerXml;
					var tr = node["TR-Name"].InnerXml;

					if (table.ContainsKey(jp)) continue;
					table.Add(jp, tr);
				}
			}
			public void LoadXml(string xmlData, string textSelector)
			{
				XmlDocument doc = new XmlDocument();
				doc.LoadXml(xmlData);

				var nodes = doc.SelectNodes(textSelector);
				foreach (XmlNode node in nodes)
				{
					var jp = node["JP-Name"].InnerXml;
					var tr = node["TR-Name"].InnerXml;

					if (table.ContainsKey(jp)) continue;
					table.Add(jp, tr);
				}
			}

			public string GetTranslation(string Name) => table.ContainsKey(Name) ? table[Name] : Name;
		}
		private class ShipTranslator : XmlTranslator
		{
			public static ShipTranslator Instance => new ShipTranslator();

			public ShipTranslator() : base(Path.Combine(TranslationsDir, "Ships.xml"), "/Ships/Ship") { }
		}
		private class EquipmentTranslator : XmlTranslator
		{
			public static EquipmentTranslator Instance => new EquipmentTranslator();

			public EquipmentTranslator() : base(Path.Combine(TranslationsDir, "Equipment.xml"), "/Equipment/Item") { }
		}
	}
}
