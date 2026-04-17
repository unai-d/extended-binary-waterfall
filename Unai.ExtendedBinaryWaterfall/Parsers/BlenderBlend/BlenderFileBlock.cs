namespace Unai.ExtendedBinaryWaterfall.Parsers.BlenderBlend;

public struct BlenderFileBlock
{
	internal long Offset;
	internal string Id;
	internal uint Size;
	internal ulong OldMemAddress;
	internal uint SdnaIndex;
	internal uint SubStructureCount;
}
