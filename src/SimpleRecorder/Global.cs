global using static SimpleRecorder.Global;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace SimpleRecorder;

internal static class Global
{
	internal static void UserExit(string msg)
	{
		Console.WriteLine($"UUser aborted, reason: {msg}. Press any key to quit.");
		Console.ReadKey();
		Environment.Exit(0);
	}
}
