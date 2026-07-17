using SixLabors.Fonts;

namespace Unai.ExtendedBinaryWaterfall.Renderers.ImageSharp;

public class ImageSharpFont : IFont
{
	internal Font _font;

	public ImageSharpFont(Font font)
	{
		_font = font;
	}
}
