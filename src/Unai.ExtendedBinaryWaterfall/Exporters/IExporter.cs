using Unai.ExtendedBinaryWaterfall.Renderers;

namespace Unai.ExtendedBinaryWaterfall.Exporters;

public interface IExporter
{
	public Generator Generator { get; set; }
	public abstract void PushNewFrame(ICanvas videoFrame, AudioBuffer audioFrame, double delta = 0.04);
	public abstract void Finish();
}
