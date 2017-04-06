using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HttpRequest = Nekoxy.HttpRequest;
using HttpResponse = Nekoxy.HttpResponse;

namespace ZTranslation.Models
{
	internal class Session
	{
		/// <summary>
		/// HTTPリクエストデータ。
		/// </summary>
		public HttpRequest Request { get; internal set; }

		/// <summary>
		/// HTTPレスポンスデータ。
		/// </summary>
		public HttpResponse Response { get; internal set; }

		public override string ToString()
			=> $"{this.Request}{Environment.NewLine}{Environment.NewLine}" +
			   $"{this.Response}";
	}
}
