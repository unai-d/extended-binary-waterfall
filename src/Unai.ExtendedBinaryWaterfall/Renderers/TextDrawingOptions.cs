using System.Drawing;

namespace Unai.ExtendedBinaryWaterfall.Renderers;

public enum VerticalAlignment
{
	Top, Center, Bottom
}

public enum HorizontalAlignment
{
	Left, Center, Right
}

public enum TextAlignment
{
	Left, Center, Right, Justified
}

public class TextDrawingOptions
{
	public PointF Origin { get; set; }
	public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;
	public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;
	public TextAlignment TextAlignment { get; set; } = TextAlignment.Left;
}
