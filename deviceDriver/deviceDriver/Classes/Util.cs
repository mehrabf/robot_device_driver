using System;
using System.Collections.Generic;
using System.Text;

namespace deviceDriver.Classes
{
	public static class Util
	{
		/// <summary>
		/// Convert String to number
		/// </summary>
		/// <param name="num">Number in string</param>
		/// <returns>converted number to int</returns>
		public static int ConvertToInt(string num)
		{
			int number = -1;
			try
			{
				number = Int32.Parse(num);
			}
			catch (FormatException e)
			{
				Console.WriteLine(e.Message);
			}
			return number;
		}
	}
}
