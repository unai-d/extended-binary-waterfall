using SixLabors.ImageSharp;

namespace Unai.ExtendedBinaryWaterfall.Exporters;

public interface IExporter
{
	public Generator Generator { get; set; }
	public abstract void PushNewFrame(Image videoFrame, AudioBuffer audioFrame, double delta = 0.04);
	public abstract void Finish();
}
