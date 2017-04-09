using System;
using System.Net;
using System.IO;

namespace ZTranslation.Models
{
	internal class HTTPRequest
	{
		public static Stream Request(string URL)
		{
			WebRequest request = HttpWebRequest.Create(URL);
			WebResponse response;

			try
			{
				response = request.GetResponse();
				return response.GetResponseStream();
			}
			catch
			{
				return null;
			}
		}
		public static Stream RequestAsync(string URL, Action<Stream> Callback)
		{
			WebRequest request = HttpWebRequest.Create(URL);
			WebResponse response;

			try
			{
				request.BeginGetResponse((x) => {
					response = request.EndGetResponse(x);
					Callback(response.GetResponseStream());
				}, null);
				return null;
			}
			catch
			{
				return null;
			}
		}
	}
}
