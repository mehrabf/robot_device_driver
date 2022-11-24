using deviceDriver.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace deviceDriver.Classes
{
	public class DeviceDriverTest
	{
		public void RunTest()
		{
			string iPAddress = "127.0.0.30";
			Console.WriteLine("Hello World!");
			IDeviceDriver robotInterface = null;
			string processID;

			// Start schedular program
			Enums.Enums.SchedulerProgram state = Enums.Enums.SchedulerProgram.ConnectionToRobot;
			// Random requested operations
			string[] executionCommands = { "pick", "home", "transfer", "pick", "home", "place", "transfer", "transfer", "transfer", "transfer", "pick" };
			// fixed sample parameters
			string[] pNames = { "Destination Location", "Source Location" };
			string[] pValues = { "5", "12" };

			// Create Device driver object 
			robotInterface = new DeviceDriver();
			int indexCmd = 0;

			// State machine
			while (true)
			{
				switch (state)
				{
					case Enums.Enums.SchedulerProgram.ConnectionToRobot:
						// connect to the robot and make sure the connection link is established before moving to the next step
						string response = robotInterface.OpenConnection(iPAddress);
						Thread.Sleep(500);
						if (response == string.Empty)
						{
							state = Enums.Enums.SchedulerProgram.Initialise;
						}
						break;
					case Enums.Enums.SchedulerProgram.Initialise:
						// put robot in ready state
						processID = robotInterface.Initialise();
						if (!string.IsNullOrWhiteSpace(processID))
						{
							Console.WriteLine("Process ID: {0}", processID);
						}
						Thread.Sleep(500);
						state = Enums.Enums.SchedulerProgram.ExecuteOperation;
						break;
					case Enums.Enums.SchedulerProgram.ExecuteOperation:
						// send random commands to the robot
						string result = robotInterface.ExecuteOperation(executionCommands[indexCmd], pNames, pValues);
						Console.WriteLine("OperationExecution: " + result);
						indexCmd++;
						if (indexCmd >= executionCommands.Length)
						{
							state = Enums.Enums.SchedulerProgram.Abort;
							indexCmd = 0;
						}
						Thread.Sleep(500);
						break;
					case Enums.Enums.SchedulerProgram.Abort:
						robotInterface.Abort();
						// Restart the process
						state = Enums.Enums.SchedulerProgram.ConnectionToRobot;
						break;
					default:
						state = Enums.Enums.SchedulerProgram.Exit;
						break;
				}

			}
		
	}
	}
}
