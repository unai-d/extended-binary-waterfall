using System.Diagnostics;

namespace Unai.ExtendedBinaryWaterfall;

public static class Logger
{
	public static ILogger DefaultLogger { get; set; } = new ConsoleLogger();

	public static void Fail(string message)
	{
		DefaultLogger.Fail(message);
	}

	public static void Error(string message)
	{
		DefaultLogger.Error(message);
	}

	public static void Warning(string message)
	{
		DefaultLogger.Warning(message);
	}

	public static void Info(string message)
	{
		DefaultLogger.Info(message);
	}

	[Conditional("DEBUG")]
	public static void Debug(string message)
	{
		DefaultLogger.Debug(message);
	}

	[Conditional("TRACE")]
	public static void Trace(string message)
	{
		DefaultLogger.Trace(message);
	}
}
