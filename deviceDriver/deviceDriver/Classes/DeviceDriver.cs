using deviceDriver.Interfaces;
using System;
using System.Text;

namespace deviceDriver.Classes
{
	public class DeviceDriver : IDeviceDriver
	{

		private enum availableCommands{home, pick, place, status};

		private IOperationManager _cmdHandler = null;

		public DeviceDriver()
		{
			// Create a command handler object
			_cmdHandler = new OperationHandler();
		}
		/// <summary>
		/// The address at which the MockRobot software is running
		/// Establish a connection with MockRobot onboard software
		/// </summary>
		/// <param name="ipAddress"></param>
		/// <returns>return process ID</returns>
		public string OpenConnection(string ipAddress)
		{
			if (_cmdHandler == null)
			{
				_cmdHandler = new OperationHandler();
			}
			_cmdHandler.Connect(ipAddress);
			if (_cmdHandler == null || !_cmdHandler.IsConnected)
			{
				return "Failed To connect to Robot!";
			}

			// Successful connection has established
			return string.Empty; // return empty string if operation succeeds 
		}
		/// <summary>
		/// This function will put the MockRobot into an automation-ready (homed) state
		/// </summary>
		/// <returns>return process id</returns>
		public string Initialise()
		{
			if (_cmdHandler == null || !_cmdHandler.IsConnected)
			{
				return "There is no connection with the Robot!";
			}

			return _cmdHandler.SetAutomationReady().ToString();
		}
		/// <summary>
		/// Checks the requested operation and then execute the valid commands from schedulerprogram
		/// </summary>
		/// <param name="operation">Operation/Command</param>
		/// <param name="parameterNames">Command's parameter's names</param>
		/// <param name="parameterValues">Command's parameter's values</param>
		/// <returns>reply message from robot/ processID </returns>
		public string ExecuteOperation(string operation, string[] parameterNames, string[] parameterValues)
		{
			// Check if instance is connected to the Robot
			if (_cmdHandler == null || !_cmdHandler.IsConnected)
			{
				return "There is no connection with the Robot!";
			}
			
			// Convert to lower case
			operation = operation.ToLower();

			// Check if the operation is valid
			if (!_cmdHandler.IsValidOperation(operation))
			{
				return $"Invalid Requested Operation! op:{operation}";
			}
			// Check if the operation's parameter are correct
			if (!_cmdHandler.IsValidCmdParameters(operation, ref parameterNames, ref parameterValues))
			{
				return $"Invalid Number of Parameters! op: {operation} pnames: {string.Join(", ", parameterNames)} pvalues:{string.Join(", ", parameterValues)}";
			}
			// Check if the operation is in progress: 
			if (_cmdHandler.IsRobotAvailable(_cmdHandler.CurrentProcessID) == false)
			{
				return $"Robot is not available! Currently processing \"{_cmdHandler.CurrentProcessID}\""; // Cannot override the current process
			}

			return _cmdHandler.ExecuteCommand(operation, parameterNames, parameterValues);
		}
		/// <summary>
		/// Terminate communication with the MockRobot closing the socket and clean up 
		/// </summary>
		/// <returns>return empty string for successful Abort</returns>
		public string Abort()
		{
			// Check if the process is running
			if (_cmdHandler == null || !_cmdHandler.IsConnected)
			{
				return "There is no connection with the Robot!";
			}
			_cmdHandler.Dispose();
			_cmdHandler = null;
			Console.WriteLine("Abort!");
			return string.Empty;
		}

	}
}
