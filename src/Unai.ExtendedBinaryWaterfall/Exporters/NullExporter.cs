using Unai.ExtendedBinaryWaterfall.Renderers;

namespace Unai.ExtendedBinaryWaterfall.Exporters;

[Exporter("null", "Null/Dummy Output", "Do nothing with the generated video. Useful for debugging purposes.")]
public class NullExporter : IExporter
{
	public Generator Generator { get; set; }

	public void Finish()
	{
		
	}

	public void PushNewFrame(ICanvas videoFrame, AudioBuffer audioFrame, double delta = 0.04)
	{
		
	}
}
