using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using Unai.ExtendedBinaryWaterfall.Renderers;

namespace Unai.ExtendedBinaryWaterfall;

public static class Extensions
{
	public static void DrawProgressBar(this ICanvas ictx, float percent, int x1, int x2, float y)
	{
		percent = Math.Clamp(percent, 0, 1);

		ictx.DrawLine(
				new PointF(x1, y),
				new PointF(x2, y),
				Color.FromArgb(32, 32, 32),
				8
			).DrawLine(
				new PointF(x1, y),
				new PointF(x1 + (x2 - x1) * percent, y),
				Color.Silver,
				8
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
}
