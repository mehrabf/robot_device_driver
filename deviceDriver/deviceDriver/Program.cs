using deviceDriver.Classes;
using deviceDriver.Interfaces;
using System;
using System.Threading;

namespace deviceDriver
{
	class Program
	{
		static void Main(string[] args)
		{
			DeviceDriverTest testDeviceDriver = new DeviceDriverTest();
			testDeviceDriver.RunTest();
		}
	}
}
