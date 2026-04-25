using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Unai.ExtendedBinaryWaterfall.Parsers.Zip;

[Parser("zip", "ZIP Archive", [ ".zip", ".apk", ".msix", ".epub", ".jar", ".war", ".docx", ".xlsx", ".pptx", ".odt", ".ods", ".odp", ".pk3", ".pk4" ])]
public class ZipParser : IParser
{
	public Stream InputStream { get; set; }
	public Stream AuxiliaryInputStream { get; set; }
	public bool SkipDirectories { get; set; } = false;

	public long EocdOffset { get; private set; } = 0;
	public long CentralDirectoryOffset { get; private set; } = 0;

	public IEnumerable<SubFile> GetSubFiles()
	{
		using var br = new BinaryReader(InputStream, Encoding.UTF8, true);

		// Find End of Central Directory (EOCD) Offset
		br.BaseStream.Position = br.BaseStream.Length - 22;
		while (br.BaseStream.Position > br.BaseStream.Length - 0xffff + 22)
		{
			var eocdSig = br.ReadUInt32();
			if (eocdSig == 0x06054b50)
			{
				EocdOffset = br.BaseStream.Position - 4;
				break;
			}
			br.BaseStream.Position -= 8;
		}

		if (EocdOffset == 0)
		{
			Logger.Error("Cannot parse ZIP file: cannot find EOCD signature.");
			yield break;
		}

		var eocdDiskNumber = br.ReadUInt16();
		var eocdDiskCdirStart = br.ReadUInt16();
		var eocdCdirCount = br.ReadUInt16();
		var eocdCdirTotalCount = br.ReadUInt16();
		var eocdCdirSize = br.ReadUInt32();
		var eocdCdirStart = br.ReadUInt32();
		var eocdCommentSize = br.ReadUInt16();
		var eocdComment = eocdCommentSize > 0 ? br.ReadString(eocdCommentSize) : null;

		CentralDirectoryOffset = eocdCdirStart;

		yield return new("End of Central Directory", EocdOffset, 22 + eocdCommentSize) { IconString = "🔶" };
		yield return new("Central Directory", eocdCdirStart, eocdCdirSize) { IconString = "🔶" };

		// Central Directory
		br.BaseStream.Position = CentralDirectoryOffset;

		while (br.BaseStream.Position < eocdCdirStart + eocdCdirSize)
		{
			var cdirOfs = br.BaseStream.Position;
			var cdirSig = br.ReadUInt32(); // 0x02014b50 / "PK\1\2"
			if (cdirSig != 0x02014b50)
			{
				Logger.Error($"Cannot read central directory record: invalid signature ({cdirSig:X8})");
				break;
			}

			var cdirMadeByVer = br.ReadUInt16();
			var cdirMinDecodeVer = br.ReadUInt16();
			var cdirFlags = br.ReadUInt16();
			var cdirCompressionAlgo = br.ReadUInt16();
			var cdirLastWrite = br.ReadUInt32(); // MS-DOS format time, then date
			var cdirCrc32 = br.ReadUInt32();
			var cdirCompressedSize = br.ReadUInt32();
			var cdirUncompressedSize = br.ReadUInt32();
			var cdirFileNameSize = br.ReadUInt16();
			var cdirExtraFieldSize = br.ReadUInt16();
			var cdirFileCommentSize = br.ReadUInt16();
			var cdirFileDiskNumber = br.ReadUInt16();
			var cdirFileAttrInt = br.ReadUInt16();
			var cdirFileAttrExt = br.ReadUInt32();
			var cdirLocalFileHdrOfs = br.ReadUInt32();
			var cdirFileName = br.ReadString(cdirFileNameSize);
			var cdirExtraField = br.ReadBytes(cdirExtraFieldSize);
			var cdirFileComment = br.ReadString(cdirFileCommentSize);

			var cdirNextOfs = br.BaseStream.Position;

			Logger.Debug($"Dir. Rec at 0x{cdirOfs:X8}: '{cdirFileName}' {cdirCompressedSize} bytes (uncomp. {cdirUncompressedSize})");

			// Local File Header
			br.BaseStream.Position = cdirLocalFileHdrOfs;
			var lfhSig = br.ReadUInt32(); // 0x04034b50 / "PK\3\4"
			br.BaseStream.Position += 22;
			var lfhFileNameSize = br.ReadUInt16();
			var lfhExtraFieldSize = br.ReadUInt16();

			var fileDataOfs = br.BaseStream.Position + lfhFileNameSize + lfhExtraFieldSize;
			var isDir = cdirFileName[^1] == '/';

			if (!(isDir && SkipDirectories)) yield return new(cdirFileName.TrimEnd('/'), fileDataOfs, cdirCompressedSize) { IsDirectory = isDir };

			br.BaseStream.Position = cdirNextOfs;
		}
	}
}