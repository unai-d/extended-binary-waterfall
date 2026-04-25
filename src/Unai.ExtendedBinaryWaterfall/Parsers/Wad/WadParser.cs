using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Unai.ExtendedBinaryWaterfall.Parsers.Wad;

[Parser("wad", "Doom Engine's Asset Archive (WAD)", [ ".wad" ])]
public class WadParser : IParser
{
	public static string[] MapLumps =
	[
		"THINGS",
		"LINEDEFS",
		"SIDEDEFS",
		"VERTEXES",
		"SEGS",
		"SSECTORS",
		"NODES",
		"SECTORS",
		"REJECT",
		"BLOCKMAP",
		"BEHAVIOR", // Hexen
	];

	public Stream InputStream { get; set; }
	public Stream AuxiliaryInputStream { get; set; }

	public IEnumerable<SubFile> GetSubFiles()
	{
		using var br = new BinaryReader(InputStream, Encoding.ASCII, true);

		var wadMagic = br.ReadBytes(4); // IWAD / PWAD
		var wadLumpCount = br.ReadUInt32();
		var wadDirectoryOff = br.ReadUInt32();

		yield return new("WAD Header", 0, 8) { IconString = "🔶" };
		yield return new("WAD Directory", wadDirectoryOff, br.BaseStream.Length - wadDirectoryOff) { IconString = "🔶" };

		br.BaseStream.Position = wadDirectoryOff;

		string currentMap = null;
		bool isPixelData = false; // flat or sprite

		for (int i = 0; i < wadLumpCount; i++)
		{
			var lumpDataOff = br.ReadUInt32();
			var lumpDataLen = br.ReadUInt32();
			var lumpName = br.ReadString(8).TrimEnd('\0');

			if (lumpDataLen == 0 && ((lumpName[0] == 'E' && lumpName[2] == 'M') || lumpName.StartsWith("MAP")))
			{
				currentMap = lumpName;
			}
			else if (!MapLumps.Contains(lumpName))
			{
				currentMap = null;
			}
			
			if (lumpName == "S_START" || lumpName == "F_START")
			{
				isPixelData = true;
			}
			else if (lumpName == "S_END" || lumpName == "F_END")
			{
				isPixelData = false;
			}
			
			if (lumpDataOff + lumpDataLen > 0)
			{
				var subfile = new SubFile(currentMap != null ? $"{currentMap}/{lumpName}" : lumpName, lumpDataOff, lumpDataLen);
				if (isPixelData) subfile.IconString = Utils.GetFileTypeEmojiFromExtension(".bmp");
				yield return subfile;
			}
		}
	}
}