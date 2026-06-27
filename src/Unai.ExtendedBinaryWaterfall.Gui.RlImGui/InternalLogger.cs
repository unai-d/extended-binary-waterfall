using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Unai.ExtendedBinaryWaterfall.Gui.RlImGui;

public class InternalLogger : ConsoleLogger
{
	DateTime _startDt = DateTime.Now;

	public static List<LogEntry> History { get; } = [];

	protected override void Print(string message, LogLevel logLevel, StackFrame sf)
	{
		base.Print(message, logLevel, sf);

		var callingMethod = sf?.GetMethod();
		var declaringType = callingMethod.DeclaringType?.Name;
		
		History.Add(new((DateTime.Now - _startDt).TotalSeconds, logLevel, callingMethod.Name, message));
	}
}
