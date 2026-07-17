using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Unai.ExtendedBinaryWaterfall;

public static class Extensions
{
	public static void DrawProgressBar(this IImageProcessingContext ictx, float percent, int x1, int x2, float y)
	{
		percent = Math.Clamp(percent, 0, 1);

		ictx.DrawLine(new(), new SolidBrush(Color.FromRgb(32, 32, 32)), 8,
				new PointF(x1, y),
				new PointF(x2, y)
			).DrawLine(new(), new SolidBrush(Color.Silver), 8,
				new PointF(x1, y),
				new PointF(x1 + (x2 - x1) * percent, y)
			);
	}

	public static string ReadString(this BinaryReader br, int length, Encoding textEncoding = null)
	{
		textEncoding ??= Encoding.ASCII;
		return textEncoding.GetString(br.ReadBytes(length));
	}

	public static string ReadCString(this BinaryReader br)
	{
		StringBuilder sb = new();

		byte b;
		while ((b = br.ReadByte()) != 0)
		{
			sb.Append((char)b);
		}

		return sb.ToString();
	}

	public static BinaryReader SkipCString(this BinaryReader br, int count = 1)
	{
		int skipCount = 0;
		while (skipCount < count)
		{
			Console.Error.WriteLine($"{skipCount}/{count} {Utils.GetBufferHexString(br, 16)}");
			if (br.ReadByte() == 0) skipCount++;
		}
		return br;
	}

	static Dictionary<int, Image<Rgba32>> _textRenderCache = [];

	public static IImageProcessingContext DrawTextAndCache(this IImageProcessingContext ctx, DrawingOptions drawingOptions, RichTextOptions textOptions, string text, Brush brush, Pen pen)
	{
		if (string.IsNullOrEmpty(text)) return ctx;

		int hash = 0x91f_c28a;
		if (brush != null) hash ^= brush.GetHashCode();
		if (pen != null) hash ^= pen.StrokeFill.GetHashCode();
		foreach (var c in text)
		{
			hash <<= 2;
			hash ^= c * 0xc10_48f1;
		}
		hash ^= 0x183 * (int)textOptions.Font.Size;

		if (!_textRenderCache.TryGetValue(hash, out var cachedTextRender))
		{
			Logger.Trace($"Generating cached version of text '{text}'…");

			// Avoiding an `ArgumentNullException` from `TextOptions..ctor`. Blame this line of code:
			// https://github.com/SixLabors/Fonts/blob/d74f3fae7250cf3a76f43780abea6e15ec40b75e/src/SixLabors.Fonts/TextOptions.cs#L32C66-L32C86
			textOptions.FallbackFontFamilies ??= [];

			var newTextOpts = new RichTextOptions(textOptions)
			{
				Origin = new System.Numerics.Vector2(0, 0),
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Top,
			};

			var imgBounds = TextMeasurer.MeasureBounds(text, newTextOpts);
			Logger.Trace($"  Measured raster bounds: {imgBounds}");
			cachedTextRender = new Image<Rgba32>((int)Math.Ceiling(imgBounds.Width + imgBounds.X), (int)Math.Ceiling(imgBounds.Height + imgBounds.Y));
			cachedTextRender.Mutate(ctx2 => ctx2.DrawText(drawingOptions, newTextOpts, text, brush, pen));

			_textRenderCache.Add(hash, cachedTextRender);
		}

		var x = textOptions.Origin.X;
		var y = textOptions.Origin.Y;
		
		if (textOptions.HorizontalAlignment == HorizontalAlignment.Center)
		{
			x -= cachedTextRender.Width / 2;
		}
		else if (textOptions.HorizontalAlignment == HorizontalAlignment.Right)
		{
			x -= cachedTextRender.Width;
		}
		if (textOptions.VerticalAlignment == VerticalAlignment.Center)
		{
			y -= cachedTextRender.Height / 2;
		}
		else if (textOptions.VerticalAlignment == VerticalAlignment.Bottom)
		{
			y -= cachedTextRender.Height;
		}

		return ctx.DrawImage(cachedTextRender, new Point((int)x, (int)y), 1f);
	}

	public static IImageProcessingContext DrawTextAndCache(this IImageProcessingContext ctx, RichTextOptions textOptions, string text, Color color)
	{
		return ctx.DrawTextAndCache(new(), textOptions, text, new SolidBrush(color), null);
	}
}
