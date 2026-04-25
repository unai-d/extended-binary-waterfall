using System.Collections.Generic;
using System.IO;

namespace Unai.ExtendedBinaryWaterfall.Parsers;

public interface IParser
{
	public Stream InputStream { get; set; }
	public Stream AuxiliaryInputStream { get; set; }
	public IEnumerable<SubFile> GetSubFiles();
}
