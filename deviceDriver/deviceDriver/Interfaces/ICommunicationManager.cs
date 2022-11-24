using System;
using System.Collections.Generic;
using System.Text;
using static deviceDriver.Classes.TcpClientAsync;

namespace deviceDriver.Interfaces
{
	interface ICommunicationManager
	{
		event DataReceivedEventHandler OnDataRecievedEvent;
		event OnConnectEventHandler OnConnectEvent;
		bool IsConnected { get; }
		bool Connect(bool enableBlocking = false);
		bool SendMessage(byte[] data);
		bool SendMessage(string data);
		void Dispose();

	}
}
