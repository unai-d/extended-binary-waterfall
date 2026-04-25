using System.IO;

namespace Unai.ExtendedBinaryWaterfall.Parsers.Elf;

public class ElfSectionHeader
{
	public long Offset;

	public uint NameStringOffset;
	public ElfSectionType Type;
	public ulong Flags;
	public ulong SectionVirtualAddress;
	public ulong SectionOffset;
	public ulong SectionSize;
	public uint LinkSectionIndex;
	public uint InfoSectionIndex;
	public ulong AddressAlign;
	public ulong EntrySize;

	public string GetName(BinaryReader br, long stringTableSecOfs)
	{
		if (NameStringOffset == 0) return null;

		var origOfs = br.BaseStream.Position;

		br.BaseStream.Position = stringTableSecOfs + NameStringOffset;

		var ret = br.ReadCString();

		br.BaseStream.Position = origOfs;

		return ret;
	}
}
