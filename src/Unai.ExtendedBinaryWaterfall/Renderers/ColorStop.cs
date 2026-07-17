using System.Drawing;

namespace Unai.ExtendedBinaryWaterfall.Renderers;

public struct ColorStop
{
	public float Location;
	public Color Color;

	public ColorStop(float location, Color color)
	{
		Location = location;
		Color = color;
	}
}
