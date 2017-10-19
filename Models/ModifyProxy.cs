using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;
using System.Net;
using TrotiNet;
using HttpRequest = Nekoxy.HttpRequest;
using HttpResponse = Nekoxy.HttpResponse;

namespace ZTranslation.Models
{
	internal class ModifyProxy : ProxyLogic
	{
		private Session currentSession;

		static new public ModifyProxy CreateProxy(HttpSocket clientSocket) => new ModifyProxy(clientSocket);
		public ModifyProxy(HttpSocket clientSocket) : base(clientSocket) { }

		public static Func<Session, byte[], byte[]> BeforeResponse = null;

		/// <summary>
		/// SendResponseをoverrideし、リクエストデータを読み取る。
		/// </summary>
		protected override void SendRequest()
		{
			this.currentSession = new Session();
			//HTTPリクエストヘッダ送信
			this.SocketPS.WriteBinary(Encoding.ASCII.GetBytes(
				$"{this.RequestLine.RequestLine}\r\n{this.RequestHeaders.HeadersInOrder}\r\n"));

			byte[] request = null;
			if (this.State.bRequestHasMessage)
			{
				if (this.State.bRequestMessageChunked)
					this.SocketBP.TunnelChunkedDataTo(this.SocketPS);

				else
				{
					request = new byte[this.State.RequestMessageLength];
					try { this.SocketBP.TunnelDataTo(request, this.State.RequestMessageLength); } catch { }
					try { this.SocketPS.TunnelDataTo(this.TunnelPS, request); } catch { }
				}
			}
			this.currentSession.Request = new HttpRequest(this.RequestLine, this.RequestHeaders, request);
			this.State.NextStep = this.ReadResponse;
		}

		/// <summary>
		/// OnReceiveResponseをoverrideし、レスポンスデータを読み取る。
		/// </summary>
		protected override void OnReceiveResponse()
		{
			#region Nekoxy Code
			if (this.ResponseStatusLine.StatusCode != 200) return;

			var response = this.ResponseHeaders.IsUnknownLength()
				? this.GetContentWhenUnknownLength()
				: this.GetContent();
			this.State.NextStep = null;

			using (var ms = new MemoryStream())
			{
				var stream = this.GetResponseMessageStream(response);
				stream.CopyTo(ms);
				var content = ms.ToArray();
				this.currentSession.Response = new HttpResponse(this.ResponseStatusLine, this.ResponseHeaders, content);
			}
			#endregion

			if (BeforeResponse != null)
			{
				response = BeforeResponse?.Invoke(this.currentSession, this.currentSession.Response.Body);
				this.ResponseHeaders.ContentEncoding = ""; // remove gzip and chunked...
			}

			#region Nekoxy Code
			this.ResponseHeaders.TransferEncoding = null;
			this.ResponseHeaders.ContentLength = (uint)response.Length;

			this.SendResponseStatusAndHeaders(); //クライアントにHTTPステータスとヘッダ送信
			this.SocketBP.TunnelDataTo(this.TunnelBP, response); //クライアントにレスポンスボディ送信

			if (!this.State.bPersistConnectionPS)
			{
				this.SocketPS?.CloseSocket();
				this.SocketPS = null;
			}
			#endregion
		}

		private byte[] GetContentWhenUnknownLength()
		{
			var buffer = new byte[512];
			this.SocketPS.TunnelDataTo(ref buffer); // buffer の長さは内部で調整される
			return buffer;
		}
	}
}