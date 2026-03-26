using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Unai.ExtendedBinaryWaterfall.Parsers.Custom;

[Parser("custom", "Unknown Format, Custom File Listing", [])]
public class CustomParser : IParser
{
	public Stream InputStream { get; set; }
	public Stream AuxiliaryInputStream { get; set; }

	public IEnumerable<SubFile> GetSubFiles()
	{
		if (AuxiliaryInputStream == null) yield break;

		using var sr = new StreamReader(AuxiliaryInputStream);
		var csvValues = sr.ReadToEnd()
			.Split('\n')
			.Select(line => line.Split(','));

		foreach (var row in csvValues.Skip(1))
		{
			if (row.Length < 4) continue;
			yield return new(row[3], long.Parse(row[0]), long.Parse(row[1]));
		}
	}
}
