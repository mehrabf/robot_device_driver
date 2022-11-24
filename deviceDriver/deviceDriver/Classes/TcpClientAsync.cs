using deviceDriver.Interfaces;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using static deviceDriver.Enums.Enums;

namespace deviceDriver.Classes
{
	public class TcpClientAsync: ICommunicationManager, IDisposable
	{
		public delegate void DataReceivedEventHandler(string data);                 // Async method to notify the Recieved Data
		public delegate void OnConnectEventHandler(RobotStatus status);             // Async method to notify the connection Status of Socket
		public event DataReceivedEventHandler OnDataRecievedEvent;
		public event OnConnectEventHandler OnConnectEvent;

		private IPAddress _ipAddress;                       // Server/MockRobot IP address
		private int _remotePort;                            // Server/MockRobot port
		private Socket _socket = null;
		private Packet _socketData = null;
		private volatile bool _disposed = false;
		private object _lock = new object();
		private static ManualResetEvent _connectDone = new ManualResetEvent(false);

		// flag to check if the socket is connected
		public bool IsConnected
		{
			get
			{
				if (_socket != null)
				{
					return _socket.Connected;
				}
				return false;
			}
		}
		public TcpClientAsync(string ip, int port)
		{
			// Get the IP Address and Port number for the MockRobot
			this._ipAddress = IPAddress.Parse(ip);
			this._remotePort = port;
		}
		/// <summary>
		/// Connect to the robot through tcp/ip socket
		/// </summary>
		/// <param name="enableBlocking"></param>
		/// <returns>return true successful connection</returns>
		public bool Connect(bool enableBlocking = false)
		{
			bool result = false;
			try
			{
				Disconnect(); // Make sure the socket is not open from previous process
				_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp); // Create a socket object TCP/IP
				if (enableBlocking == true)
				{
					_connectDone.Reset();
				}

				IPEndPoint endPointServer = new IPEndPoint(this._ipAddress, _remotePort); // Define MockRobot ip address and port										
				_socket.Blocking = false; // Choose non blocking process to connect to the MockRobot
				_socket.BeginConnect(endPointServer, new AsyncCallback(OnConnect), _socket);
				if (enableBlocking == true)
				{
					_connectDone.WaitOne(); // Wait till the connection recieve signal.
				}
				Thread.Sleep(20);
				result = true;
			}
			catch (Exception ex)
			{
				OnConnectEvent(RobotStatus.Disconnected);
				Console.WriteLine($"Failed Connecting to Socket! IP = {this._ipAddress}, Port = {this._remotePort}: " + ex.ToString());
			}
			return result;

		}
		/// <summary>
		/// Sending string message to the robot
		/// </summary>
		/// <param name="data">message to robot</param>
		/// <returns>return true if message has been send to the robot successfully</returns>
		public bool SendMessage(string data)
		{
			if (this.IsConnected)
			{
				// Convert string to bytes array
				Byte[] byteDateLine = Encoding.ASCII.GetBytes(data.ToCharArray());
				return SendMessage(byteDateLine);
			}
			Console.WriteLine($"Failed to write! Connection is disrupted! IP = {this._ipAddress}, Port = {this._remotePort}: ");
			return false;			
		}
		/// <summary>
		/// Sending array of bytes message to the robot
		/// </summary>
		/// <param name="data">message to robot</param>
		/// <returns>return true if message has been send to the robot successfully</returns>
		public bool SendMessage(byte[] data)
		{

			if (this.IsConnected)
			{
				try
				{
					// Send data as array of bytes
					_socket.Send(data, data.Length, 0);
					return true;
				}
				catch (Exception ex)
				{
					Dispose();
					//throw new Exception("Failed to write!" + ex.Message);
					Console.WriteLine("Failed to write!" + ex.Message);
				}
			}
			return false;
		}
		/// <summary>
		/// Disconnect from the socket
		/// </summary>
		/// <param name="forceDisconnect"> Enable to force shuting down the socket</param>
		public void Disconnect(bool forceDisconnect = true)
		{
			// Disconnect the socket gracefully
			if (_socket != null && _socket.Connected)
			{
				if (forceDisconnect == true)
				{
					OnConnectEvent(RobotStatus.Disconnected);
					// Force to close the socket
					_socket.Shutdown(SocketShutdown.Both);
					Thread.Sleep(10);
					_socket.Disconnect(false);
					_socket.Close();
				}
				else
				{
					// Reusing the socket
					_socket.Shutdown(SocketShutdown.Both);
					Thread.Sleep(10);
					_socket.Disconnect(true);
				}
			}
		}
		/// <summary>
		/// Retrieve the socket from the state object.
		/// </summary>
		/// <param name="ar"></param>
		private void OnConnect(IAsyncResult ar)
		{
			// Retrieve the socket from the state object.
			if (_socket.Connected)
			{
				// Setting up the callbacks handler if Socket is Connected
				dataRecieveCallback();
				OnConnectEvent(RobotStatus.Connected);
			}
			else
			{
				OnConnectEvent(RobotStatus.Disconnected);
				Console.WriteLine("Failed to establish socket connection!");
			}

		}

		/// <summary>
		/// Setup Recieve Callback for Async Listning
		/// </summary>
		private void dataRecieveCallback()
		{
			try
			{
				Packet pack = new Packet(_socket);
				_socket.BeginReceive(pack.DataBuffer, 0, pack.DataBuffer.Length, SocketFlags.None, new AsyncCallback(OnRecievedData), pack);
			}
			catch (Exception ex)
			{
				Dispose();
				throw new Exception("Recieve Callback Setup Failed!: " + ex.Message);
			}
		}
		/// <summary>
		/// Recieve packet data from TCP server
		/// </summary>
		/// <param name="ar"></param>		
		private void OnRecievedData(IAsyncResult ar)
		{
			_socketData = (Packet)ar.AsyncState;
			try
			{
				SocketError errorCode;
				if (_socket != null)
				{
					// Check data is available
					int nByteReceived = _socketData.CurrentSocket.EndReceive(ar, out errorCode);
					if (errorCode != SocketError.Success)
					{
						nByteReceived = 0;
					}
					else
					{
						if (nByteReceived > 0)
						{
							// Convert to string and trigger a DataRecievedEvent
							if (this.OnDataRecievedEvent != null)
							{
								this.OnDataRecievedEvent.Invoke(Encoding.ASCII.GetString(_socketData.DataBuffer, 0, nByteReceived));
							}
							// If the connection still available make sure the we listen to incoming data
							if (_socket != null && _socket.Connected)
							{
								dataRecieveCallback();
							}
						}
					}
				}
			}
			catch (ObjectDisposedException)
			{
				Dispose();
				//Console.WriteLine("OnRecievedData, ObjectDisposedException: Socket has been closed!" + ex.Message);
				//threw new("OnRecievedData, ObjectDisposedException: Socket has been closed!" + ex.Message);
			}
			catch (SocketException)
			{
				Dispose();
				//Console.WriteLine("OnRecievedData, SocketException: Socket has been closed!" + se.Message);
				//threw new("OnRecievedData, SocketException: Socket has been closed!" + se.Message);
			}
		}

		public void Dispose()
		{
			// Close the socket and call the GC
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			lock (_lock)
			{
				if (!_disposed)
				{
					if (disposing)
					{
						if (_socket != null)
						{
							Disconnect();
							_socket.Dispose();
						}
					}
					_socket = null;
					_disposed = true;
				}
			}
		}
		~TcpClientAsync()
		{
			Dispose(false);
		}
	}

	public class Packet
	{
		private const int BUFFER_SIZE = 1024;               // Data receive buffer size
		public Socket CurrentSocket;						// Current socket being used
		public byte[] DataBuffer = new byte[BUFFER_SIZE];	// received data buffer
		
		public Packet(Socket sock)
		{
			// Create packet object
			CurrentSocket = sock;
		}
	}
}
