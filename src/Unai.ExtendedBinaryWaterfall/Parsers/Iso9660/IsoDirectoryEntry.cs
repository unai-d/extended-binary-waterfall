using System.Text;

namespace Unai.ExtendedBinaryWaterfall.Parsers.Iso9660;

class IsoDirectoryEntry
{
	public string Name;
	public int DataLba;
	public int DataLength;
	public byte Flags;
	public Encoding TextEncoding = Encoding.ASCII;

	public bool IsDirectory => (Flags & 0b10) != 0;
}
