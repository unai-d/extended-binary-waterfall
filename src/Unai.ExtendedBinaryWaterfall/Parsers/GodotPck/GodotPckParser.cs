using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Unai.ExtendedBinaryWaterfall.Parsers.GodotPck;

[Parser("godot", "Godot Pack Archive", [".pck"])]
public class GodotPckParser : IParser
{
	public static uint MagicNumber = 0x43504447; // GDPC

	public Stream InputStream { get; set; }
	public Stream AuxiliaryInputStream { get; set; }
	public long GlobalOffset { get; set; } = 0;

	public GodotPckParser()
	{

	}

	internal GodotPckParser(Stream inputStream, long globalOfs = 0)
	{
		InputStream = inputStream;
		GlobalOffset = globalOfs;
	}

	public IEnumerable<SubFile> GetSubFiles()
	{
		using BinaryReader br = new(InputStream, Encoding.ASCII, true);

		br.BaseStream.Position = GlobalOffset;

		var pckMagic = br.ReadUInt32();
		if (pckMagic != MagicNumber)
		{
			throw new InvalidDataException($"Invalid Godot pack magic number: 0x{pckMagic:x8} ≠ 0x{MagicNumber:x8}");
		}

		var pckVerPack = br.ReadUInt32();
		var pckVerMajor = br.ReadUInt32();
		var pckVerMinor = br.ReadUInt32();
		var pckVerRev = br.ReadUInt32();

		Logger.Debug($"PCK version {pckVerPack}.{pckVerMajor}.{pckVerMinor}.{pckVerRev}");

		uint pckFlags = 0;
		long pckFileBase = -1;
		long pckFileBaseOfs = -1;
		long pckFileTable = -1;
		long pckFileTableOfsRaw = -1;

		if (pckVerPack >= (uint)GodotPckVersion.Godot4)
		{
			pckFlags = br.ReadUInt32();
			pckFileBaseOfs = br.BaseStream.Position;
			pckFileBase = br.ReadInt64();

			if (pckVerPack >= (uint)GodotPckVersion.Godot4_5)
			{
				pckFileTableOfsRaw = br.BaseStream.Position;
				pckFileTable = br.ReadInt64();
			}
		}

		br.BaseStream.Position += 16 * sizeof(int); // TODO: what's in here?

		if (pckVerPack < (uint)GodotPckVersion.Godot4_5)
		{
			pckFileTable = br.BaseStream.Position;
		}

		Logger.Debug($"PCK file table ofs.: 0x{pckFileTableOfsRaw:x12}");

		var pckFileTableOfs = br.BaseStream.Position = GlobalOffset + pckFileTable;

		var pckFileCount = br.ReadUInt32();

		Logger.Debug($"PCK file count: {pckFileCount}");

		// assumming non-encrypted PCK.

		List<GodotPckSubFile> subFiles = [];
		for (int i = 0; i < pckFileCount; i++)
		{
			var fPathLen = br.ReadInt32();
			var fPath = Encoding.UTF8.GetString(br.ReadBytes(fPathLen)).TrimEnd('\0');
			long fOfsOfs = br.BaseStream.Position;
			long fOfs = br.ReadInt64();
			long fSize = br.ReadInt64();
			byte[] fHash = br.ReadBytes(16); // MD5

			uint flags = 0;
			if (pckVerPack >= (int)GodotPckVersion.Godot4)
			{
				flags = br.ReadUInt32();
			}

			Logger.Debug($"PCK file: \"{fPath}\" at 0x{fOfs:x12}");

			var duplicate = subFiles.FirstOrDefault(sf => sf.Offset == fOfs);
			if (duplicate != null)
			{
				Logger.Debug($"  Last file is a duplicate of \"{duplicate.Path}\".");
				duplicate.Path = fPath;
			}
			else
			{
				subFiles.Add(new() { Path = fPath, Offset = fOfs, Length = fSize });
			}
		}

		// Return everything.

		yield return new("PCK Header", GlobalOffset, 5 * sizeof(int)) { IconString = "🔶" };

		yield return new("PCK File Table", pckFileTableOfs, br.BaseStream.Position - pckFileTableOfs) { IconString = "🔶" };

		foreach (var subFile in subFiles)
		{
			yield return new(subFile.Path, GlobalOffset + subFile.Offset, subFile.Length);
		}
	}
}

public class GodotPckSubFile()
{
	public string Path { get; set; }
	public long Offset { get; set; }
	public long Length { get; set; }
}