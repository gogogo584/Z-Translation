using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZTranslation.Models
{
	internal class SafeTcpServer : TrotiNet.TcpServer
	{
		public SafeTcpServer(int listeningPort, bool isUseIPv6) : base(listeningPort, isUseIPv6) { }

		public void Shutdown()
		{
			base.Stop();

			foreach (var socket in ConnectedSockets.Values.ToArray())
			{
				this.CloseSocket(socket);
			}
		}

		public new void Stop()
		{
			Shutdown();
		}
	}
}
