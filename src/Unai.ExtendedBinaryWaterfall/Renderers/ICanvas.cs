using System.Drawing;

namespace Unai.ExtendedBinaryWaterfall.Renderers;

public interface ICanvas
{
	int Width { get; }
	int Height { get; }

	ICanvas Clear(Color color);
	ICanvas Resize(int width, int height);
	ICanvas FlipVertical();
	ICanvas DrawLine(PointF p1, PointF p2, Color color);
	ICanvas DrawLine(PointF p1, PointF p2, Color color, float thickness);
	ICanvas DrawText(IFont font, string text, PointF origin, Color color);
	ICanvas DrawText(IFont font, TextDrawingOptions opt, string text, Color color);
	ICanvas DrawImage(PointF p1, ICanvas image);
	void CopyPixelDataTo(byte[] buf);
	ICanvas FillRectangleGradient(RectangleF rect, PointF p1, PointF p2, params ColorStop[] gsteps);
}
