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
						#region api_start2
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
									if (x.api_name != null) x.api_name = getTranslation("ShipType", x.api_id.ToString());

								// Furniture names
								foreach (var x in svdata.api_data.api_mst_furniture)
								{
									if (x.api_title != null) x.api_title = getTranslation("Furniture", x.api_title.ToString());
									if (x.api_description != null) x.api_description = getTranslation("Furniture", x.api_description.ToString());
								}

								// Useitem names
								foreach (var x in svdata.api_data.api_mst_useitem)
								{
									if (x.api_name != null) x.api_name = getTranslation("UseItem", x.api_name.ToString());
									if (x.api_description != null) x.api_description[0] = getTranslation("UseItem", x.api_description[0].ToString());
								}
								// Payitem names
								foreach (var x in svdata.api_data.api_mst_payitem)
								{
									if (x.api_name != null) x.api_name = getTranslation("PayItem", x.api_name.ToString());
									if (x.api_description != null) x.api_description = getTranslation("PayItem", x.api_description.ToString());
								}

								// MapArea
								foreach (var x in svdata.api_data.api_mst_maparea)
									if (x.api_name != null) x.api_name = getTranslation("MapArea", x.api_name.ToString());

								foreach (var x in svdata.api_data.api_mst_mapinfo)
								{
									if (x.api_name != null) x.api_name = getTranslation("MapArea", x.api_name.ToString());
									if (x.api_opetext != null) x.api_opetext = getTranslation("MapArea", x.api_opetext.ToString());
									if (x.api_infotext != null) x.api_infotext = getTranslation("MapArea", x.api_infotext.ToString());
								}

								// Expedition
								foreach (var x in svdata.api_data.api_mst_mission)
								{
									if (x.api_name != null) x.api_name = getTranslation("Expedition", x.api_name.ToString());
									if (x.api_details != null) x.api_details = getTranslation("Expedition", x.api_details.ToString());
								}

								// BGM
								foreach (var x in svdata.api_data.api_mst_bgm)
									if (x.api_name != null) x.api_name = getTranslation("BGM", x.api_name.ToString());
							}
							raw_content = JsonConvert.SerializeObject(svdata, serializeOption);
							data = Encoding.UTF8.GetBytes("svdata=" + raw_content);
							break;
						#endregion

						#region Jukebox list request
						case "/kcsapi/api_req_furniture/music_list":
							raw_content = Encoding.UTF8.GetString(data).Substring("svdata=".Length);
							svdata = JObject.Parse(raw_content);
							{
								foreach (var x in svdata.api_data)
								{
									if (x.api_name != null) x.api_name = getTranslation("BGM", x.api_name.ToString());
									if (x.api_description != null) x.api_description = getTranslation("BGM", x.api_description.ToString());
								}
							}
							raw_content = JsonConvert.SerializeObject(svdata, serializeOption);
							data = Encoding.UTF8.GetBytes("svdata=" + raw_content);
							break;
						#endregion

						#region Dictionary screen
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
										if (x.api_sinfo != null) x.api_sinfo = getTranslation("ShipLibraryText", x.api_index_no.ToString());
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
						#endregion

						#region Quest page
						case "/kcsapi/api_get_member/questlist":
							raw_content = Encoding.UTF8.GetString(data).Substring("svdata=".Length);
							svdata = JObject.Parse(raw_content);
							{
								foreach (var x in svdata.api_data.api_list)
								{
									var id = x.api_no.ToString();
									var output = QuestTranslator.Instance.GetTranslation("Name" + id);
									if (output != null)
									{
										x.api_title = QuestTranslator.Instance.GetTranslation("Name" + id);
										x.api_detail = QuestTranslator.Instance.GetTranslation("Detail" + id);
									}
								}
							}
							raw_content = JsonConvert.SerializeObject(svdata, serializeOption);
							data = Encoding.UTF8.GetBytes("svdata=" + raw_content);
							break;
						#endregion
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
			Action<string, string, XmlTranslator> RemoteLoader = (name, url, translator) =>
			{
				var urlBase = "https://raw.githubusercontent.com/WolfgangKurz/Z-Translation/master/Translations/";
				new Thread(() =>
				{
					string xml = "";

					try
					{
						HttpWebRequest rq = WebRequest.Create(urlBase + url) as HttpWebRequest;
						rq.Timeout = 5000;
						HttpWebResponse response = rq.GetResponse() as HttpWebResponse;

						using (var reader = new StreamReader(response.GetResponseStream()))
							xml = reader.ReadToEnd();

						translator.Load(xml, "/Texts/Text");
						Translators.TryAdd(name, translator);
					}
					catch { }
				}).Start();
			};

			Translators = new ConcurrentDictionary<string, XmlTranslator>();

			// XML based loader (Z-Translation datas)
			RemoteLoader("ShipName", "ShipName.xml", new XmlTranslator());
			RemoteLoader("ShipType", "ShipType.xml", ShipTypeTranslator.Instance);
			RemoteLoader("ShipGetMessage", "ShipGetMessage.xml", new XmlTranslator());
			RemoteLoader("ShipLibraryText", "ShipLibraryText.xml", new XmlTranslator());
			RemoteLoader("EquipmentName", "EquipmentName.xml", new XmlTranslator());
			RemoteLoader("EquipmentType", "EquipmentType.xml", new XmlTranslator());
			RemoteLoader("EquipmentInfo", "EquipmentInfo.xml", new XmlTranslator());
			RemoteLoader("Furniture", "Furniture.xml", new XmlTranslator());
			RemoteLoader("UseItem", "UseItem.xml", new XmlTranslator());
			RemoteLoader("PayItem", "PayItem.xml", new XmlTranslator());
			RemoteLoader("MapArea", "MapArea.xml", new XmlTranslator());
			RemoteLoader("Expedition", "Expedition.xml", new XmlTranslator());
			RemoteLoader("BGM", "BGM.xml", new XmlTranslator());

			// Prepare KC3 quest datas
			QuestTranslator.Instance.Prepare();
		}


		// XML based data loader (Basic class)
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

			protected virtual void ValueProcessor(ref string jp, ref string tr, XmlNode node) { }

			private string ValueAdjust(string Value) => Value
				.Replace("<br />", "<br>")
				.Replace("&amp;", "&");
			private void LoadTexts(XmlDocument doc, string textSelector)
			{
				var nodes = doc.SelectNodes(textSelector);
				foreach (XmlNode node in nodes)
				{
					var jp = ValueAdjust(node["JP-Name"]?.InnerXml);
					var tr = ValueAdjust(node["TR-Name"]?.InnerXml);
					ValueProcessor(ref jp, ref tr, node);

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

			public virtual string GetTranslation(string Name) => table.ContainsKey(Name) ? table[Name] : Name;
		}

		// XML based loader (Z-Translation datas)
		private class ShipTypeTranslator : XmlTranslator
		{
			public static ShipTypeTranslator Instance { get; } = new ShipTypeTranslator();

			protected override void ValueProcessor(ref string jp, ref string tr, XmlNode node)
			{
				int id = -1;
				int.TryParse(node["ID"]?.InnerText, out id);

				jp = id.ToString();
				if (id == 8) tr = "고속전함";
				else if (id == 9) tr = "전함";
			}
		}

		// JSON based loader (KC3)
		private class QuestTranslator
		{
			private ConcurrentDictionary<string, string> table { get; set; }
			public static QuestTranslator Instance { get; } = new QuestTranslator();

			public QuestTranslator()
			{
				this.table = new ConcurrentDictionary<string, string>();
			}

			public void Prepare()
			{
				var dataUrl = "https://raw.githubusercontent.com/KC3Kai/kc3-translations/master/data/kr/quests.json";
				new Thread(() =>
				{
					string data = "";

					try
					{
						HttpWebRequest rq = WebRequest.Create(dataUrl) as HttpWebRequest;
						rq.Timeout = 5000;
						HttpWebResponse response = rq.GetResponse() as HttpWebResponse;

						using (var reader = new StreamReader(response.GetResponseStream()))
							data = reader.ReadToEnd();

						var svdata = JObject.Parse(data);
						foreach (var quest in svdata.Children<JProperty>())
						{
							var id = -1;
							int.TryParse(quest.Name, out id);

							this.table.TryAdd("Name" + id.ToString(), quest.Value["name"].ToString());
							this.table.TryAdd("Detail" + id.ToString(), quest.Value["desc"].ToString());
						}
					}
					catch { }
				}).Start();
			}
			public virtual string GetTranslation(string Name) => table.ContainsKey(Name) ? table[Name] : null;
		}
	}
}
