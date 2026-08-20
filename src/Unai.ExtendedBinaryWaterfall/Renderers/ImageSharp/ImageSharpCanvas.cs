using System.Linq;
using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using Color = System.Drawing.Color;
using PointF = System.Drawing.PointF;

namespace Unai.ExtendedBinaryWaterfall.Renderers.ImageSharp;

public class ImageSharpCanvas : ICanvas
{
	internal ImageSharpRenderer _renderer;
	internal Image<Rgba32> _image;

	public int Width { get => _image.Width; }

	public int Height { get => _image.Height; }

	public ImageSharpCanvas(ImageSharpRenderer renderer, Image<Rgba32> image)
	{
		_renderer = renderer;
		_image = image;
	}

	public ICanvas Clear(Color color)
	{
		_image.Mutate(ictx => ictx.Clear(color.ToImageSharp()));
		return this;
	}

	public ICanvas DrawImage(PointF p1, ICanvas image)
	{
		_image.Mutate(ictx => ictx.DrawImage(((ImageSharpCanvas)image)._image, new Point((int)p1.X, (int)p1.Y), 1f));
		return this;
	}

	public ICanvas DrawLine(PointF p1, PointF p2, Color color)
	{
		return DrawLine(p1, p2, color, 1);
	}

	public ICanvas DrawLine(PointF p1, PointF p2, Color color, float thickness)
	{
		_image.Mutate(ictx => ictx.DrawLine(new(), new SolidBrush(color.ToImageSharp()), thickness, (Vector2)p1, (Vector2)p2));
		return this;
	}

	public ICanvas DrawText(IFont font, TextDrawingOptions opt, string text, Color color)
	{
		var isfont = (ImageSharpFont)font;

		_image.Mutate(ictx => ictx.DrawText(new RichTextOptions(isfont._font)
		{
			Origin = (Vector2)opt.Origin,
			HorizontalAlignment = opt.HorizontalAlignment.ToImageSharp(),
			VerticalAlignment = opt.VerticalAlignment.ToImageSharp(),
			TextAlignment = opt.TextAlignment.ToImageSharp(),
			FallbackFontFamilies = [ _renderer._emojiFontFamily ]
		}, text, new SolidBrush(color.ToImageSharp())));

		return this;
	}

	public ICanvas DrawText(IFont font, string text, PointF origin, Color color)
	{
		return DrawText(font, new TextDrawingOptions { Origin = origin }, text, color);
	}

	public ICanvas FlipVertical()
	{
		_image.Mutate(ctx => ctx.Flip(FlipMode.Vertical));
		return this;
	}

	public ICanvas Resize(int width, int height)
	{
		_image.Mutate(ictx => ictx.Resize(width, height, new NearestNeighborResampler()));
		return this;
	}

	public void CopyPixelDataTo(byte[] buf)
	{
		_image.CopyPixelDataTo(buf);
	}

	public ICanvas FillRectangleGradient(System.Drawing.RectangleF rect, PointF p1, PointF p2, params ColorStop[] colorStops)
	{
		var isColorStops = colorStops.Select(gs => new SixLabors.ImageSharp.Drawing.Processing.ColorStop(gs.Location, gs.Color.ToImageSharp())).ToArray();

		var linearGradientBrush = new LinearGradientBrush(
				p1.ToImageSharp(),
				p2.ToImageSharp(),
				GradientRepetitionMode.None,
				isColorStops
			);

		_image.Mutate(ictx => ictx.Fill(linearGradientBrush, rect.ToImageSharp()));

		return this;
	}
}
