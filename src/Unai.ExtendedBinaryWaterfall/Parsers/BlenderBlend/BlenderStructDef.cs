using System;
using System.Collections.Generic;

namespace Unai.ExtendedBinaryWaterfall.Parsers.BlenderBlend;

public struct BlenderStructDef
{
	internal List<Tuple<int, int>> Fields;
	internal ushort FieldCount;
	internal ushort TypeIndex;
}
