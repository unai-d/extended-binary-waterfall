using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Unai.ExtendedBinaryWaterfall.Parsers.Elf;

[Parser("elf", "Executable and Linkable Format (ELF)", [ ".elf", ".out", ".o", ".so", ".ko", ".mod", ".prx" ])]
public class ElfParser : IParser
{
	public Stream InputStream { get; set; }
	public Stream AuxiliaryInputStream { get; set; }

	private List<ElfSectionHeader> _sectionHeaders = new();

	public IEnumerable<SubFile> GetSubFiles()
	{
		using BinaryReader br = new(InputStream, Encoding.ASCII, true);

		var elfMagic = br.ReadBytes(4); // Funny var name :)
		var elfClass = br.ReadByte();
		var is64Bit = elfClass == 2;
		var elfEndianness = br.ReadByte(); // 2 = big-endian
		if (elfEndianness == 2)
		{
			Logger.Error($"Cannot parse big-endian ELF file: not supported yet.");
			yield break;
		}
		var elfVersion = br.ReadByte(); // always 1
		var elfAbi = br.ReadByte();
		var elfAbiVersion = br.ReadByte();
		br.BaseStream.Position += 7; // zero-filled padding

		var elfObjType = br.ReadUInt16();
		var elfIsa = br.ReadUInt16();
		var elfVersion32 = br.ReadUInt32(); // always 1
		var elfEntryPointAddr = is64Bit ? br.ReadUInt64() : br.ReadUInt32();
		var elfPhtOfs = is64Bit ? br.ReadUInt64() : br.ReadUInt32();
		var elfShtOfs = is64Bit ? br.ReadUInt64() : br.ReadUInt32();
		var elfFlags = br.ReadUInt32();
		var elfHdrSize = br.ReadUInt16();
		var elfPhtEntrySize = br.ReadUInt16();
		var elfPhtEntryCount = br.ReadUInt16();
		var elfShtEntrySize = br.ReadUInt16();
		var elfShtEntryCount = br.ReadUInt16();
		var elfShtStringTableIndex = br.ReadUInt16();

		yield return new("ELF Header", 0, br.BaseStream.Position) { IconString = "🔶" };

		for (int phIdx = 0; phIdx < elfPhtEntryCount; phIdx++)
		{
			br.BaseStream.Position = (long)elfPhtOfs + (phIdx * elfPhtEntrySize);
			var phOfs = br.BaseStream.Position;
			
			var phType = br.ReadUInt32();
			var phFlags = 0u; if (is64Bit) phFlags = br.ReadUInt32();
			var phSegOfs = is64Bit ? br.ReadUInt64() : br.ReadUInt32();
			var phSegVa = is64Bit ? br.ReadUInt64() : br.ReadUInt32();
			var phSegPa = is64Bit ? br.ReadUInt64() : br.ReadUInt32();
			var phSegSize = is64Bit ? br.ReadUInt64() : br.ReadUInt32();
			var phSegSizeInMem = is64Bit ? br.ReadUInt64() : br.ReadUInt32();
			if (!is64Bit) phFlags = br.ReadUInt32();
			var phAlign = is64Bit ? br.ReadUInt64() : br.ReadUInt32();

			yield return new($"Program Header {phIdx}", phOfs, elfPhtEntrySize) { IconString = "🔶" };
		}

		for (int shIdx = 0; shIdx < elfShtEntryCount; shIdx++)
		{
			br.BaseStream.Position = (long)elfShtOfs + (long)(shIdx * elfShtEntrySize);
			var shOfs = br.BaseStream.Position;

			_sectionHeaders.Add(new()
			{
				Offset = shOfs,
				NameStringOffset = br.ReadUInt32(),
				Type = (ElfSectionType)br.ReadUInt32(),
				Flags = is64Bit ? br.ReadUInt64() : br.ReadUInt32(),
				SectionVirtualAddress = is64Bit ? br.ReadUInt64() : br.ReadUInt32(),
				SectionOffset = is64Bit ? br.ReadUInt64() : br.ReadUInt32(),
				SectionSize = is64Bit ? br.ReadUInt64() : br.ReadUInt32(),
				LinkSectionIndex = br.ReadUInt32(),
				InfoSectionIndex = br.ReadUInt32(),
				AddressAlign = is64Bit ? br.ReadUInt64() : br.ReadUInt32(),
				EntrySize = is64Bit ? br.ReadUInt64() : br.ReadUInt32(),
			});
		}

		var stringTableSecHdr = _sectionHeaders[elfShtStringTableIndex];

		foreach (var sh in _sectionHeaders)
		{
			if (sh.Type == ElfSectionType.Null) continue;

			var name = sh.GetName(br, (long)stringTableSecHdr.SectionOffset) ?? $"Section {sh.Type}";

			yield return new($"Section Header {name}", sh.Offset, elfShtEntrySize) { IconString = "🔶" };
			yield return new(name, (long)sh.SectionOffset, (long)sh.SectionSize);
		}
	}
}