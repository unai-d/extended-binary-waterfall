using System;
using System.Runtime.CompilerServices;

namespace Unai.ExtendedBinaryWaterfall.Renderers.ImageSharp;

public static class ImageSharpExtensions
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SixLabors.ImageSharp.Color ToImageSharp(this System.Drawing.Color color)
	{
		return SixLabors.ImageSharp.Color.FromRgba(color.R, color.G, color.B, color.A);
	}

	public static SixLabors.ImageSharp.PointF ToImageSharp(this System.Drawing.PointF point)
	{
		return new SixLabors.ImageSharp.PointF(point.X, point.Y);
	}

	public static SixLabors.ImageSharp.RectangleF ToImageSharp(this System.Drawing.RectangleF rect)
	{
		return new SixLabors.ImageSharp.RectangleF(rect.Left, rect.Top, rect.Width, rect.Height);
	}

	public static SixLabors.Fonts.HorizontalAlignment ToImageSharp(this HorizontalAlignment i)
	{
		return i switch
		{
			HorizontalAlignment.Left => SixLabors.Fonts.HorizontalAlignment.Left,
			HorizontalAlignment.Right => SixLabors.Fonts.HorizontalAlignment.Right,
			HorizontalAlignment.Center => SixLabors.Fonts.HorizontalAlignment.Center,
			_ => throw new InvalidOperationException()
		};
	}

	public static SixLabors.Fonts.VerticalAlignment ToImageSharp(this VerticalAlignment i)
	{
		return i switch
		{
			VerticalAlignment.Top => SixLabors.Fonts.VerticalAlignment.Top,
			VerticalAlignment.Bottom => SixLabors.Fonts.VerticalAlignment.Bottom,
			VerticalAlignment.Center => SixLabors.Fonts.VerticalAlignment.Center,
			_ => throw new InvalidOperationException()
		};
	}

	public static SixLabors.Fonts.TextAlignment ToImageSharp(this TextAlignment i)
	{
		return i switch
		{
			TextAlignment.Left => SixLabors.Fonts.TextAlignment.Start,
			TextAlignment.Center => SixLabors.Fonts.TextAlignment.Center,
			TextAlignment.Right => SixLabors.Fonts.TextAlignment.End,
			_ => throw new InvalidOperationException()
		};
	}
}
