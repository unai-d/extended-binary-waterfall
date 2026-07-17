namespace Unai.ExtendedBinaryWaterfall.Renderers;

public interface IRenderer
{
	void Initialize();
	ICanvas CreateCanvas(int width, int height);
	ICanvas CreateCanvas(byte[] buf, int width, int height);
	IFont CreateFont(string fontName, float size);
}
