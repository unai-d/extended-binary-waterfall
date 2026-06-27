namespace Unai.ExtendedBinaryWaterfall.Gui.RlImGui;

public struct LogEntry
{
	public double TimestampOffset;
	public ConsoleLogger.LogLevel LogLevel;
	public string StackMethodName;
	public string Message;

	public LogEntry(double timestampOfs, ConsoleLogger.LogLevel logLevel, string stackMethod, string message)
	{
		TimestampOffset = timestampOfs;
		LogLevel = logLevel;
		StackMethodName = stackMethod;
		Message = message;
	}
}
