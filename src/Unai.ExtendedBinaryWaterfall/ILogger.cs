namespace Unai.ExtendedBinaryWaterfall;

public interface ILogger
{
	public void Fail(string message);
	public void Error(string message);
	public void Warning(string message);
	public void Info(string message);
	public void Debug(string message);
	public void Trace(string message);
}
