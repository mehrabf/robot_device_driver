namespace deviceDriver.Interfaces
{
	public interface IOperationManager
	{
		int CurrentProcessID { get; }
		bool IsConnected { get; }
		bool Connect(string ipAddress);
		int SetAutomationReady();
		void Close();
		bool IsValidOperation(string cmd);
		bool IsRobotAvailable(int processId);
		string GetRobotStatus(int processId);
		bool IsValidCmdParameters(string operation, ref string[] parameterNames, ref string[] parameterValues);
		string ExecuteCommand(string operation, string[] parameterNames, string[] parameterValues);
		void Dispose();
	}
}
