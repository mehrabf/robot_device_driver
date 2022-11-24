using System;
using System.Collections.Generic;
using System.Text;

namespace deviceDriver.Interfaces
{
	interface IDeviceDriver
	{
		string OpenConnection(string IPAddress);
		string Initialise();
		string ExecuteOperation(string operation, string[] parameterNames, string[] parameterValues);
		string Abort();
	}
}
