using deviceDriver.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using static deviceDriver.Enums.Enums;

namespace deviceDriver.Classes
{
	public class OperationHandler: IOperationManager, IDisposable
	{

		TcpClientAsync.DataReceivedEventHandler _onRecieveHandler = null;
		TcpClientAsync.OnConnectEventHandler _onConnectHandler = null;
		private static readonly string[] _validOperations = { "pick", "place", "transfer" };
		private ConcurrentQueue<string> _receiveMsgQueue = new ConcurrentQueue<string>();
		private static ReaderWriterLock _msgQueueLocker = new ReaderWriterLock();
		private static ICommunicationManager _robotComm = null;
		private StringBuilder _sb = new StringBuilder();
		private readonly object _eventLock = new object();
		private string _replyMessage;
		private volatile bool _disposed = false;
		private object _lock = new object();

		public string IPAddress;
		public int Port = 1000;

		public int CurrentProcessID { get; private set; } = -1;
		public bool IsConnected
		{
			get
			{
				if (_robotComm != null)
				{
					return _robotComm.IsConnected;
				}
				return false;
			}
		}

		public OperationHandler()
		{

		}
		/// <summary>
		/// Connect to the Robot by openning the socket port
		/// </summary>
		/// <param name="ipAddress">IP Address</param>
		/// <returns>Connection result</returns>
		public bool Connect(string ipAddress)
		{
			// Store the current IP Address
			this.IPAddress = ipAddress;

			// Create a tcp client object with requested ip address and port
			_robotComm = new TcpClientAsync(this.IPAddress, this.Port);
			// connect async event handlers
			_onConnectHandler = new TcpClientAsync.OnConnectEventHandler(OnConnect);
			_onRecieveHandler = new TcpClientAsync.DataReceivedEventHandler(OnRecieved);
			lock (_eventLock)
			{
				_robotComm.OnConnectEvent += _onConnectHandler;
			}
			lock (_eventLock)
			{
				_robotComm.OnDataRecievedEvent += _onRecieveHandler;
			}
			// Connect to the socket
			return _robotComm.Connect();
		}

		/// <summary>
		/// This commands will put the MockRobot into an automation-ready (homed) state.
		/// </summary>
		/// <returns>Process ID</returns>		
		public int SetAutomationReady()
		{
			string replyMsg = "";
			int processID = -1;		// Failed to get the process ID
			
			// Init MockRobot and set it to automation-Ready state.
			if (_sendCommand("home%", ref replyMsg, timeout: 100))
			{
				processID = _getProcessID(replyMsg);
			}

			this.CurrentProcessID = processID;

			return processID; 
		}

		/// <summary>
		/// Close the communication port and unsubscribe the event
		/// </summary>
		public void Close()
		{
			// 
			if (_robotComm != null)
			{
				lock (_eventLock)
				{
					_robotComm.OnDataRecievedEvent -= _onRecieveHandler;
				}
				Thread.Sleep(10);
				_robotComm.Dispose();
				lock (_eventLock)
				{
					_robotComm.OnConnectEvent -= _onConnectHandler;
				}
			}
		}
		/// <summary>
		/// Check the requested command is valid
		/// </summary>
		/// <param name="cmd">Operation/Command</param>
		/// <returns>return true if the operation is valid</returns>
		public bool IsValidOperation(string cmd)
		{

			if (_validOperations.Any(cmd.Contains))
			{
				return true;
			}
			return false;
		}

		/// <summary>
		/// Check the number of parameters are correct
		/// </summary>
		/// <param name="operation">Operation/Command</param>
		/// <param name="parameterNames">Command's parameters name</param>
		/// <param name="parameterValues">Command's parameters values</param>
		/// <returns>returns true if the operation parameters are correct</returns>
		public bool IsValidCmdParameters(string operation, ref string[] parameterNames, ref string[] parameterValues)
		{
			bool result = false;

			if(operation == "pick" || operation == "place")
			{
				if (parameterNames.Length == 1 && parameterValues.Length == 1)
				{
					result = true;
				}
			}
			else if (operation == "transfer")
			{
				if (parameterValues.Length == 2 && parameterValues.Length == 2)
				{
					result = true;
				}
			}

			return result;
		}
		/// <summary>
		///  Check availibility of the robot
		///		1- Get the current process id
		///		2- Send status command
		///		3- Check if the process is finished
		///
		///	Reply message for Status
		///		- In Progress					--> NOT Available
		///		- Finished Successfully         -->		Available
		///		- Terminated With Error			-->		Available
		///		- No reply message				--> NOT Available
		///		- No found substring			--> NOT Available
		/// </summary>
		/// <param name="processId">Process ID</param>
		/// <returns>return true if the robot is available to accespt new commands</returns>
		public bool IsRobotAvailable(int processId)
		{
			/*

				*/
			string replyMessage = GetRobotStatus(processId);
			// If there is no reply message, Assuming the MockRobot is not available.
			// If the substring cannot be found in the MockRobot reply message to the status command then the assumption is the MockRobot is not available.
			string[] _robotAvailableStatus = { "Finished Successfully", "Terminated With Error" };

			if (!string.IsNullOrWhiteSpace(replyMessage))
			{
				if (_robotAvailableStatus.Any(replyMessage.Contains))
				{
					return true;
				}
			}
			return false;
		}
		/// <summary>
		/// Get the Robot Status by sending status%processID
		/// Expected Reply message: 
		///		- In Progress
		///		- Finished Successfully
		///		- Terminated With Error
		/// </summary>
		/// <param name="processId">Process ID</param>
		/// <returns>reply message from robot</returns>
		public string GetRobotStatus(int processId)
		{
			string replyMsg = "";
			string statusCmd = $"status%{processId}";
			string[] _expectedStatusCommands = { "In Progress", "Finished Successfully", "Terminated With Error" };

			if (!_sendCommand(statusCmd, ref replyMsg, ref _expectedStatusCommands, timeout: 2000))
			{
				// ToDo: We can retry sending the commands to the MockRobot
				;
			}
			return replyMsg;
		}
		/// <summary>
		/// Generic function to execute requested valid commands from schedulerprogram
		/// </summary>
		/// <param name="operation">Operation/Command</param>
		/// <param name="parameterNames">Command's parameter's names</param>
		/// <param name="parameterValues">Command's parameter's values</param>
		/// <returns>reply message from robot/ processID </returns>
		public string ExecuteCommand(string operation, string[] parameterNames, string[] parameterValues)
		{
			// get the operation name
			string replyMsg = string.Empty;
			string cmd = $"{operation}%";

			int idx = 0;

			// Add parameters to the cmd
			foreach (string par in parameterNames)
			{
				cmd += $"{par}{parameterValues[idx]}";
				idx++;
			}

			// Robot will immediately return a unique ID that can be used to track 
			if (_sendCommand(cmd, ref replyMsg, timeout: 100))
			{
				// get the current process id
				this.CurrentProcessID = _getProcessID(replyMsg);
			}

			return replyMsg;
		}

		/// <summary>
		/// Send the command to the MockRobot without checking reply messages
		/// </summary>
		/// <param name="cmd">Operation/Command</param>
		/// <param name="timeout">Timeout</param>
		/// <returns>return true if the sending command is successful </returns>
		private bool _sendCommand(string cmd, int timeout = 10)
		{
			bool result = false;
			if (_robotComm != null)
			{
				result = _robotComm.SendMessage(cmd);
				Thread.Sleep(timeout);              // Apply delay to fill the recieve buffer
			}
			return result;
		}
		/// <summary>
		/// Send the command to the MockRobot and wait to get reply messages
		/// </summary>
		/// <param name="cmd">Operation/Command</param>
		/// <param name="replyMsg">Received message</param>
		/// <param name="timeout">Timeout</param>
		/// <param name="retry">Number of retry</param>
		/// <returns>return true if the sending command is successful </returns>
		private bool _sendCommand(string cmd, ref string replyMsg, int timeout = 100, int retry = 3)
		{
			bool result = false;
			if (_robotComm != null)
			{
				for (int i = 0; i < retry; i++)
				{
					// Send the command and check the result
					if (_robotComm.SendMessage(cmd))
					{
						Thread.Sleep(timeout);
						// Get the received buffer messages including any reply message from the MockRobot.
						replyMsg = _receiveMessage();
						// Check if there is any response from Mock Robot
						if (!string.IsNullOrWhiteSpace(replyMsg))
						{
							result = true;
							break;
						}
					}
					else
					{   // If unsuccessful sending commands then delay for 100ms
						Thread.Sleep(100);
					}
				}
			}
			return result;
		}
		/// <summary>
		/// Send the command and get the reply message within given timeout and check the expected substrings
		/// </summary>
		/// <param name="cmd">Operation/Command</param>
		/// <param name="replyMsg">Recieved message</param>
		/// <param name="expectedSubString">Expected substring</param>
		/// <param name="timeout">Timeout</param>
		/// <param name="retry">Number of retry</param>
		/// <returns>return true if the sending command is successful </returns>
		private bool _sendCommand(string cmd, ref string replyMsg, ref string[] expectedSubString, int timeout = 100, int retry = 3)
		{
			bool result = false;
			if (_robotComm != null)
			{
				for (int i = 0; i < retry; i++)
				{
					// Send the command and check the result
					if (_robotComm.SendMessage(cmd))
					{
						Thread.Sleep(timeout);
						// Get the received buffer messages including any reply message from the MockRobot.
						replyMsg = _receiveMessage();
						// Check if there is any response from Mock Robot
						if (!string.IsNullOrWhiteSpace(replyMsg))
						{
							// Check if there is any expected substring within the reply message
							if (expectedSubString.Any(replyMsg.Contains))
							{
								// received correct reply message
								result = true;
								break;
							}
						}
					}
					else
					{   // If unsuccessful sending commands then delay for 100ms
						Thread.Sleep(100);
					}
				}
			}
			return result;
		}
		/// <summary>
		///  Add the received messages to the concurrentqueue for further processing
		/// </summary>
		/// <param name="isEnableAppend">disable/enable clearing the buffer</param>
		/// <returns>received message from robot</returns>
		private string _receiveMessage(bool isEnableAppend = false)
		{
			try
			{
				_msgQueueLocker.AcquireWriterLock(int.MaxValue);
				if (this._receiveMsgQueue.Count > 0)
				{
					// by default the stringbuilder buffer will be cleared before append new message from the queue.
					if (isEnableAppend == false)
					{
						_sb.Clear();
					}
					while (this._receiveMsgQueue.TryDequeue(out _replyMessage))
					{
						_sb.Append(_replyMessage);
					}
					return _sb.ToString();
				}
			}
			finally
			{
				_msgQueueLocker.ReleaseWriterLock();
			}
			return String.Empty;
		}
		/// <summary>
		/// convert received reply message to the process id
		/// </summary>
		/// <param name="replyMsg"> Status command response from the robot</param>
		/// <returns>process id</returns>
		private int _getProcessID(string replyMsg)
		{
			return Util.ConvertToInt(replyMsg);
		}
		/// <summary>
		/// display the socket connection status
		/// </summary>
		/// <param name="status"> Socket connection status</param>
		private void OnConnect(RobotStatus status)
		{
			Console.WriteLine("Connection Status : " + status.ToString());
		}
		/// <summary>
		/// Add received messages from robot to the queue
		/// </summary>
		/// <param name="data"> Received reply message from the Robot</param>
		private void OnRecieved(string data)
		{
			// Asynchoronous method to receive incoming messages from MockRobot
			try
			{
				_msgQueueLocker.AcquireWriterLock(int.MaxValue);
				_receiveMsgQueue.Enqueue(data.Trim());
			}
			finally
			{

				_msgQueueLocker.ReleaseWriterLock();
			}
			Console.WriteLine("Recieved Data : " + data);
		}
		/// <summary>
		///  Dispose the object
		/// </summary>
		public void Dispose()
		{
			// Close the socket and call the GC
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		/// <summary>
		/// Close the communication port
		/// </summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			lock (_lock)
			{
				if (!_disposed)
				{
					if (disposing)
					{
						if (_robotComm != null)
						{
							Close();
							_robotComm.Dispose();
						}
					}
					_robotComm = null;
					_disposed = true;
				}
			}
		}
		~OperationHandler()
		{
			Dispose(false);
		}
	}
}
