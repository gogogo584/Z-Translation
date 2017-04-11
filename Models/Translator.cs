using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Xml;
using System.IO;
using System.Net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ZTranslation.Models
{
	// XML based data loader (Basic class)
	internal class XmlTranslator
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
			if (xmlPath.StartsWith("<?")) doc.LoadXml(xmlPath);
			else doc.Load(xmlPath);
			LoadTexts(doc, textSelector);
		}

		public virtual string GetTranslation(string Name) => table.ContainsKey(Name) ? table[Name] : Name;
	}

	// XML based loader (Z-Translation datas)
	internal class ShipTypeTranslator : XmlTranslator
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
	internal class QuestTranslator
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
