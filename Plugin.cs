using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Web;
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
	[ExportMetadata("Version", "0.0.1.2")]
	[ExportMetadata("Author", "WolfgangKurz")] // wolfgangkurzdev@gmail.com
	[ExportMetadata("AuthorURL", "http://swaytwig.com/")]
	public class Plugin : IPlugin
	{
		internal static int ProxyPort => 40729;

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

		private IEnumerable<BinaryPatcher> BinaryPatchers { get; set; }
		private void PrepareBinaryPatchers()
		{
			var patchers = new List<BinaryPatcher>();
			patchers.Add(FontPatcher.Instance);
			patchers.Add(TitleMainPatcher.Instance);
			patchers.Add(PortMainPatcher.Instance);
			patchers.Add(OrganizeMainPatcher.Instance);
			patchers.Add(SupplyMainPatcher.Instance);

			BinaryPatchers = patchers;
		}

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
				#region swf patch
				foreach (var patcher in BinaryPatchers)
					if (patcher.Patch(session, ref data))
						return data;
				#endregion

				#region Patch HTTP requests
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
									if (x.GetType() == typeof(JValue)) continue;

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
				#endregion

				return data; // Encoding.UTF8.GetBytes(sv_data);
			};

			string ver = Common.CheckUpdate();
			if (ver != null && ver.Length > 0)
			{
				var message = string.Format("-= Z-Translation =-\n\nNew version {0} has released.\n\nOK: Open Update Webpage\nCancel: Dismiss", ver);
				var output = System.Windows.MessageBox.Show(message, "Z-Translation", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Information);

				if(output== System.Windows.MessageBoxResult.OK)
					System.Diagnostics.Process.Start(Common.UpdateWebpage);
			}

			// Disable auto-translations
			KanColleSettings.EnableTranslations.Value = false;
			KanColleClient.Current.Translations.EnableTranslations = false;

			// Prepare Translator, Binary Patcher
			PrepareTranslators();
			PrepareBinaryPatchers();

			// Start LocalProxy
			var Server = new SafeTcpServer(ProxyPort, false);
			Server.Start(ModifyProxy.CreateProxy);

			Server.InitListenFinished.WaitOne();
			if (Server.InitListenException != null)
				throw Server.InitListenException;

			Grabacr07.KanColleViewer.Application.Current.Exit += (s, e) =>
			{
				Server.Shutdown();
				new Thread(() =>
				{
					try
					{
						Thread.Sleep(3000);
						Environment.Exit(0);
					}
					catch { }
				}).Start();
			};
		}
	}
}
