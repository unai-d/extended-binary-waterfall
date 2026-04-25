using System;
using System.Diagnostics;
using Spectre.Console;

namespace Unai.ExtendedBinaryWaterfall;

public static class Logger
{
	public enum LogLevel
	{
		Fail, Error, Warning, Info, Debug, Trace
	}

	public static bool UseColor { get; set; } = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NOCOLOR"));

	public static readonly IAnsiConsole ConsoleOut = AnsiConsole.Create(new AnsiConsoleSettings()
	{
		Out = new AnsiConsoleOutput(Console.Error)
	});

	private static void Print(string message, LogLevel logLevel, StackFrame sf)
	{
		var callingMethod = sf?.GetMethod();
		var source = callingMethod != null ? $"{callingMethod.DeclaringType?.Name} {callingMethod.Name}" : "?";
		
		if (UseColor)
		{
			var logLevelColorName = logLevel switch
			{
				LogLevel.Fail => "maroon",
				LogLevel.Error => "red",
				LogLevel.Warning => "yellow",
				LogLevel.Debug => "green",
				LogLevel.Trace => "aqua",
				_ => "white",
			};
			ConsoleOut.MarkupLineInterpolated($"[gray]{source}[/] [{logLevelColorName}]{message}[/]");
		}
		else
		{
			ConsoleOut.WriteLine($"{source} {message}");
		}
	}

	public static void Fail(string message)
	{
		Print(message, LogLevel.Fail, new StackFrame(1));
	}

	public static void Error(string message)
	{
		Print(message, LogLevel.Error, new StackFrame(1));
	}

	public static void Warning(string message)
	{
		Print(message, LogLevel.Warning, new StackFrame(1));
	}

	public static void Info(string message)
	{
		Print(message, LogLevel.Info, new StackFrame(1));
	}

	[Conditional("DEBUG")]
	public static void Debug(string message)
	{
		Print(message, LogLevel.Debug, new StackFrame(1));
	}

	[Conditional("TRACE")]
	public static void Trace(string message)
	{
		Print(message, LogLevel.Trace, new StackFrame(1));
	}
}
