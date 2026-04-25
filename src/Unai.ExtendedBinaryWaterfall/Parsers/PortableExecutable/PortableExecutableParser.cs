using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Unai.ExtendedBinaryWaterfall.Parsers.PortableExecutable;

public enum PeDataDirectory
{
	ExportTable,
	ImportTable,
	ResourceTable,
	ExceptionTable,
	SecurityTable,
	BaseRelocationTable,
	Debug,
	Description,
	GlobalPointer,
	TlsTable,
	LoadConfigurationTable,
	BoundImport,
	ImportAddressTable,
	DelayImportDescriptor,
	ClrRuntimeHeader,
}

[Parser("pe", "Portable Executable", [ ".exe", ".dll", ".mui", ".sys", ".scr", ".cpl", ".ocx", ".ax", ".fon", ".efi" ])]
public class PortableExecutableParser : IParser
{
	public Stream InputStream { get; set; }
	public Stream AuxiliaryInputStream { get; set; }

	public long CoffHeaderOffset { get; private set; } = 0;
	public long CoffOptionalSectionOffset { get; private set; } = 0;
	public long DataDirectoryTableOffset { get; private set; } = 0;

	public IEnumerable<SubFile> GetSubFiles()
	{
		using BinaryReader br = new(InputStream, Encoding.ASCII, true);

		yield return new("DOS Header", 0, 0x40);

		var peDosHdrMagic = br.ReadString(2); // "MZ"
		br.BaseStream.Position = 0x3c;
		CoffHeaderOffset = br.ReadUInt32();
		br.BaseStream.Position = CoffHeaderOffset;

		yield return new("DOS Stub", 0x40, CoffHeaderOffset) { IconString = "🔶" };
		yield return new("COFF Header", CoffHeaderOffset, 0x18) { IconString = "🔶" };

		// PE COFF Header
		var peMagic = br.ReadString(4); // "PE\0\0"
		var peMachineId = br.ReadUInt16();
		var peSectionCount = br.ReadUInt16();
		var peTimestamp = br.ReadUInt32();
		var peSymTabPtr = br.ReadUInt32(); // unused
		var peSymTabCount = br.ReadUInt32(); // unused
		var peOptionalHeaderSize = br.ReadUInt16();
		var peFlags = br.ReadUInt16();
		Logger.Debug($"PE COFF Header: machine {peMachineId:X4}, {peSectionCount} sections");
		bool is64Bit = peMachineId == 0x8664;
		
		// PE Optional Header
		CoffOptionalSectionOffset = br.BaseStream.Position;
		var peOptHdrMagic = br.ReadUInt16();
		bool isPe32Plus = peOptHdrMagic == 0x020b;
		var peLinkerVerMajor = br.ReadByte();
		var peLinkerVerMinor = br.ReadByte();
		var peSizeOfCode = br.ReadUInt32();
		var peSizeOfInitData = br.ReadUInt32();
		var peSizeOfUninitData = br.ReadUInt32();
		var peEntryPointOfs = br.ReadUInt32();
		var peBaseOfCode = br.ReadUInt32();
		var peBaseOfData = isPe32Plus ? 0 : br.ReadUInt32();
		// NT-specific
		var peNtImageBase = isPe32Plus ? br.ReadUInt64() : br.ReadUInt32();
		var peNtSectionAlignment = br.ReadUInt32();
		var peNtFileAlignment = br.ReadUInt32();
		var peNtOsVerMajor = br.ReadUInt16();
		var peNtOsVerMinor = br.ReadUInt16();
		var peNtImageVerMajor = br.ReadUInt16();
		var peNtImageVerMinor = br.ReadUInt16();
		var peNtSubsysVerMajor = br.ReadUInt16();
		var peNtSubsysVerMinor = br.ReadUInt16();
		br.ReadUInt32(); // reserved
		var peNtSizeOfImage = br.ReadUInt32();
		var peNtSizeOfHeaders = br.ReadUInt32();
		var peNtChecksum = br.ReadUInt32();
		var peNtSubsystem = br.ReadUInt16();
		var peNtDllFlags = br.ReadUInt16();
		var peNtSizeOfStackReserve = isPe32Plus ? br.ReadUInt64() : br.ReadUInt32();
		var peNtSizeOfStackCommit = isPe32Plus ? br.ReadUInt64() : br.ReadUInt32();
		var peNtSizeOfHeapReserve = isPe32Plus ? br.ReadUInt64() : br.ReadUInt32();
		var peNtSizeOfHeapCommit = isPe32Plus ? br.ReadUInt64() : br.ReadUInt32();
		var peNtLoaderFlags = br.ReadUInt32();
		var peNtRvaSizePairCount = br.ReadUInt32();
		Logger.Debug($"PE Opt. Header: magic {peOptHdrMagic:X4} code size {peSizeOfCode:X8} entrypoint {peEntryPointOfs:X8}");
		Logger.Debug($"NT Header: image base {peNtImageBase:X8}, osver {peNtOsVerMajor}.{peNtOsVerMinor}, subsys {peNtSubsystem}, {peNtRvaSizePairCount} dirs");
		yield return new("COFF Optional Header", CoffOptionalSectionOffset, br.BaseStream.Position - CoffOptionalSectionOffset) { IconString = "🔶" };

		// Data Dirs.
		DataDirectoryTableOffset = br.BaseStream.Position;
		Logger.Debug("Reading PE data directories…");
		for (int i = 0; i < peNtRvaSizePairCount; i++)
		{
			var dataDirRva = br.ReadUInt32();
			var dataDirSize = br.ReadUInt32();
			if (dataDirRva != 0)
			{
				Logger.Debug($"PE data dir {i,2}: RVA {dataDirRva:X16} Size {dataDirSize}");
			}
		}
		yield return new("Data Directory Table", DataDirectoryTableOffset, br.BaseStream.Position - DataDirectoryTableOffset) { IconString = "🔶" };

		// Sections
		Logger.Debug("Reading PE section headers…");

		for (int i = 0; i < peSectionCount; i++)
		{
			var sectOfs = br.BaseStream.Position;

			var sectName = br.ReadString(8).TrimEnd('\0');
			var sectSize = br.ReadUInt32();
			var sectVirtualAddr = br.ReadUInt32();
			var sectRawDataSize = br.ReadUInt32();
			var sectRawDataPtr = br.ReadUInt32();
			var sectRelocPtr = br.ReadUInt32();
			var sectLineNumPtr = br.ReadUInt32();
			var sectRelocCount = br.ReadUInt16();
			var sectLineNumCount = br.ReadUInt16();
			var sectFlags = br.ReadUInt32();

			Logger.Debug($"PE Section: {sectName} size {sectSize:X8} vaddr {sectVirtualAddr:X8} data {sectRawDataPtr:X8}:{sectRawDataSize:X8}");
			yield return new(sectName, (long)sectVirtualAddr, (long)sectSize);

			br.BaseStream.Position = sectOfs + 40;
		}
	}
}
