using System;
using System.Diagnostics;
using Spectre.Console;

namespace Unai.ExtendedBinaryWaterfall;

public class ConsoleLogger : ILogger
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

	protected virtual void Print(string message, LogLevel logLevel, StackFrame sf)
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

	public void Fail(string message)
	{
		Print(message, LogLevel.Fail, new StackFrame(2));
	}

	public void Error(string message)
	{
		Print(message, LogLevel.Error, new StackFrame(2));
	}

	public void Warning(string message)
	{
		Print(message, LogLevel.Warning, new StackFrame(2));
	}

	public void Info(string message)
	{
		Print(message, LogLevel.Info, new StackFrame(2));
	}

	public void Debug(string message)
	{
		Print(message, LogLevel.Debug, new StackFrame(2));
	}

	public void Trace(string message)
	{
		Print(message, LogLevel.Trace, new StackFrame(2));
	}
}
