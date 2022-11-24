using System;
using System.Collections.Generic;
using System.Text;

namespace deviceDriver.Enums
{
	public class Enums
	{
		public enum RobotStatus
		{
			Disconnected, 
			Connected
		}


		public enum SchedulerProgram
		{ 
			ConnectionToRobot,
			Initialise, 
			ExecuteOperation,
			Abort,
			Exit
		}


	}
}
