using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Unai.ExtendedBinaryWaterfall.Parsers.WindowsIcon;

public class WindowsIconEntry
{
	public int Width;
	public int Height;
	public int ColorPaletteColorCount;
	public ushort ColorPlanesOrHotspotX;
	public ushort BppOrHotspotY;
	public uint DataSize;
	public uint DataOffset;
	public byte[] RgbaBitmapData;

	public byte[] GetBitmap(byte[] iconFileData = null)
	{
		using MemoryStream ms = new();
		using BinaryWriter bw = new(ms, Encoding.ASCII, true);

		// struct ICONIMAGE
		// var iconData = Data ?? iconFileData[(int)DataOffset..(int)(DataOffset + DataSize)];

		bw.Write('B');
		bw.Write('M');
		bw.Write((uint)(RgbaBitmapData.Length + 14 + 40)); // file size
		bw.Write((uint)0);
		bw.Write((uint)(14 + 40));

		// var infoHeaderOff = bw.BaseStream.Position;

		bw.Write((uint)40);
		bw.Write((uint)Width);
		bw.Write((uint)Height);
		bw.Write((ushort)1); // planes (16)
		bw.Write((ushort)BppOrHotspotY);
		bw.Write(0); // compression mode
		bw.Write(RgbaBitmapData.Length); // size of image
		bw.Write(0); // x px/m
		bw.Write(0); // y px/m
		bw.Write(0); // colors used
		bw.Write(0); // important colors

		bw.Write(RgbaBitmapData);

		return ms.ToArray();
	}
}

[Parser("ico", "Windows Icon (ICO)", [ ".ico", ".cur" ])]
public class WindowsIconParser : IParser
{
	public Stream InputStream { get; set; }
	public Stream AuxiliaryInputStream { get; set; }

	public List<WindowsIconEntry> Entries { get; } = [];

	public IEnumerable<SubFile> GetSubFiles()
	{
		foreach (var entry in Entries)
		{
			yield return new($"@{entry.DataOffset:x8}", entry.DataOffset, entry.DataSize);
		}
	}

	public void Load(byte[] input)
	{
		using MemoryStream ms = new(input, false);
		using BinaryReader br = new(ms);
		Load(br);
	}

	public void Load(BinaryReader br)
	{
		var reserved0 = br.ReadUInt16();
		var imageType = br.ReadUInt16();
		var imageCount = br.ReadUInt16();

		for (int i = 0; i < imageCount; i++)
		{
			var imageEntry = new WindowsIconEntry();

			imageEntry.Width = br.ReadByte();
			imageEntry.Height = br.ReadByte();
			imageEntry.ColorPaletteColorCount = br.ReadByte();
			_ = br.ReadByte();
			imageEntry.ColorPlanesOrHotspotX = br.ReadUInt16();
			imageEntry.BppOrHotspotY = br.ReadUInt16();
			imageEntry.DataSize = br.ReadUInt32();
			imageEntry.DataOffset = br.ReadUInt32();

			Logger.Debug($"Icon: {imageEntry.Width}×{imageEntry.Height} @{imageEntry.DataOffset:X8} {imageEntry.DataSize}");

			Entries.Add(imageEntry);
		}

		foreach (var imageEntry in Entries)
		{
			br.BaseStream.Position = imageEntry.DataOffset;

			var bmInfoHeaderSize = br.ReadUInt32();
			var bmWidth = br.ReadUInt32();
			var bmHeight = br.ReadUInt32();
			var bmPlanes = br.ReadUInt16();
			var bmBitsPerPixel = br.ReadUInt16();
			// var bmCompression = br.ReadUInt32();
			// var bmSizeOfImage = br.ReadUInt32();

			Logger.Debug($"Icon bitmap info: {bmWidth}×{bmHeight} {bmBitsPerPixel}bpp");
			
			br.BaseStream.Position = imageEntry.DataOffset + bmInfoHeaderSize;

			if (bmBitsPerPixel == 32) // BGRA, then 1-bit alpha mask
			{
				byte[] bmXorMask = br.ReadBytes((int)((bmWidth * (bmHeight / 2)) * 4));
				byte[] bmAndMask = br.ReadBytes((int)((bmWidth * (bmHeight / 2)) / 8));

				imageEntry.RgbaBitmapData = [..bmXorMask];

				// Apply AND (alpha) mask to BGRA data.
				// for (int off = 0; off < bmAndMask.Length; off++)
				// {
				// 	var maskValue8 = bmAndMask[off];
				// 	if (off % (bmWidth / 8) == 0) Console.Error.WriteLine();
				// 	Console.Error.Write($"{maskValue8:b8}");
				// 	for (int byteOff = 0; byteOff < 8; byteOff++)
				// 	{
				// 		bool maskValue = ((maskValue8 >> (8 - byteOff)) & 1) == 1;
				// 		var rgbaOff = 3 + (off * 8 + byteOff) * 4;
				// 		imageEntry.RgbaBitmapData[rgbaOff] = (byte)(maskValue ? 0xff : 0x00);
				// 		// if (off < 16) Logger.Debug($"{off}[{byteOff}] = {maskValue8:b8} {(maskValue8 >> byteOff):b8} {maskValue} → {rgbaOff}");

				// 		// Console.Error.Write($"{(maskValue?"#":".")}");
				// 	}
				// }
			}
		}
	}
}