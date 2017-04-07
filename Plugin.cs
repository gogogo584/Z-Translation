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
using System.Web;
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
				#region Font patch
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
				#endregion

				try
				{
					Func<string, string, string> getTranslation = (x, y) =>
					{
						if (y.Length == 0) return y;
						var output = Translators.ContainsKey(x) ? (Translators[x]?.GetTranslation(y) ?? y) : y;
						if (output == y) System.Diagnostics.Debug.WriteLine("{0} => {1}", x, y);
						return output;
					};

					var serializeOption = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii };
					string raw_content = null;
					dynamic svdata;

					switch (session.Request.PathAndQuery)
					{
						case "/kcsapi/api_start2":
							raw_content = Encoding.UTF8.GetString(data).Substring("svdata=".Length);
							svdata = JObject.Parse(raw_content);
							{
								// Ship names
								foreach (var x in svdata.api_data.api_mst_ship)
								{
									if (x.api_name != null) x.api_name = getTranslation("ShipName", x.api_name.ToString());
									if (x.api_getmes != null) x.api_getmes = getTranslation("ShipGetMessage", x.api_getmes.ToString());
								}

								// Slotitem names
								foreach (var x in svdata.api_data.api_mst_slotitem)
								{
									if (x.api_name != null) x.api_name = getTranslation("EquipmentName", x.api_name.ToString());
									if (x.api_info != null) x.api_info = getTranslation("EquipmentInfo", x.api_info.ToString());
								}

								// Slotitem Type names
								foreach (var x in svdata.api_data.api_mst_slotitem_equiptype)
									if (x.api_name != null) x.api_name = getTranslation("EquipmentType", x.api_name.ToString());
								// Ship Type names
								foreach (var x in svdata.api_data.api_mst_stype)
									if (x.api_name != null) x.api_name = getTranslation("ShipType", x.api_name.ToString());

								// Furniture names
								foreach (var x in svdata.api_data.api_mst_furniture)
								{
									if (x.api_title != null) x.api_title = getTranslation("FurnitureName", x.api_title.ToString());
									if (x.api_description != null) x.api_description = getTranslation("FurnitureDescription", x.api_description.ToString());
								}

								// Useitem names
								foreach (var x in svdata.api_data.api_mst_useitem)
								{
									if (x.api_name != null) x.api_name = getTranslation("UseItemName", x.api_name.ToString());
									if (x.api_description != null) x.api_description[0] = getTranslation("UseItemDescription", x.api_description[0].ToString());
								}
							}
							raw_content = JsonConvert.SerializeObject(svdata, serializeOption);
							data = Encoding.UTF8.GetBytes("svdata=" + raw_content);
							break;

						case "/kcsapi/api_get_member/picture_book":
							var s_type = HttpUtility.ParseQueryString(session.Request.BodyAsString)["api_type"];
							int n_type = 1;
							int.TryParse(s_type, out n_type);

							raw_content = Encoding.UTF8.GetString(data).Substring("svdata=".Length);
							svdata = JObject.Parse(raw_content);
							{
								foreach (var x in svdata.api_data.api_list)
								{
									if (n_type == 1) // Ship
									{
										if (x.api_name != null) x.api_name = getTranslation("ShipName", x.api_name.ToString());
										if (x.api_info != null) x.api_info = getTranslation("ShipGetMessage", x.api_getmes.ToString());
									}
									else if (n_type == 2) // Equipment
									{
										if (x.api_name != null) x.api_name = getTranslation("EquipmentName", x.api_name.ToString());
										if (x.api_info != null) x.api_info = getTranslation("EquipmentInfo", x.api_info.ToString());
									}
								}
							}
							raw_content = JsonConvert.SerializeObject(svdata, serializeOption);
							data = Encoding.UTF8.GetBytes("svdata=" + raw_content);
							break;
					}
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

					//try
					{
						HttpWebRequest rq = WebRequest.Create(urlBase + url) as HttpWebRequest;
						rq.Timeout = 5000;
						HttpWebResponse response = rq.GetResponse() as HttpWebResponse;

						using (var reader = new StreamReader(response.GetResponseStream()))
							xml = reader.ReadToEnd();

						var translator = new XmlTranslator(xml, "/Texts/Text");
						Translators.TryAdd(name, translator);
					}
					//catch { }
				}).Start();
			};

			Translators = new ConcurrentDictionary<string, XmlTranslator>();
			Translators.TryAdd("ShipName", ShipTranslator.Instance);
			Translators.TryAdd("EquipmentName", EquipmentTranslator.Instance);
			Translators.TryAdd("EquipmentType", EquipmentTypeTranslator.Instance);
			Translators.TryAdd("ShipType", ShipTypeTranslator.Instance);

			RemoteLoader("ShipGetMessage", "ShipGetMessage.xml");
			RemoteLoader("EquipmentInfo", "EquipmentInfo.xml");
			RemoteLoader("FurnitureName", "FurnitureName.xml");
			RemoteLoader("FurnitureDescription", "FurnitureDescription.xml");
			RemoteLoader("UseItemName", "UseItemName.xml");
			RemoteLoader("UseItemDescription", "UseItemDescription.xml");
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
			public XmlTranslator(string xmlPath, string textSelector = "/Texts/Text") : this()
			{
				this.Load(xmlPath, textSelector);
			}

			private string ValueAdjust(string Value) => Value
				.Replace("<br />", "<br>")
				.Replace("&amp;", "&");
			private void LoadTexts(XmlDocument doc, string textSelector)
			{
				var nodes = doc.SelectNodes(textSelector);
				foreach (XmlNode node in nodes)
				{
					var jp = ValueAdjust(node["JP-Name"].InnerXml);
					var tr = ValueAdjust(node["TR-Name"].InnerXml);

					if (table.ContainsKey(jp)) continue;
					table.Add(jp, tr);
				}
			}

			public void Load(string xmlPath, string textSelector)
			{
				XmlDocument doc = new XmlDocument();
				if(xmlPath.StartsWith("<?")) doc.LoadXml(xmlPath);
				else doc.Load(xmlPath);
				LoadTexts(doc, textSelector);
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
		private class EquipmentTypeTranslator : XmlTranslator
		{
			public static EquipmentTypeTranslator Instance => new EquipmentTypeTranslator();

			public EquipmentTypeTranslator() : base(Path.Combine(TranslationsDir, "EquipmentTypes.xml"), "/EquipmentTypes/Item") { }
		}
		private class ShipTypeTranslator : XmlTranslator
		{
			public static ShipTypeTranslator Instance => new ShipTypeTranslator();

			public ShipTypeTranslator() : base(Path.Combine(TranslationsDir, "ShipTypes.xml"), "/ShipTypes/Type") { }
		}
	}
}
