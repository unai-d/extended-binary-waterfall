using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Unai.ExtendedBinaryWaterfall.Parsers.GameMaker;

[Parser("gamemaker", "GameMaker Asset Archive", [ ".win", ".unx" ])]
public class GameMakerParser : IParser
{
	public Stream InputStream { get; set; }
	public Stream AuxiliaryInputStream { get; set; }
	
	private GameMakerChunk _rootChunk = null;

	private void ParseInputStream()
	{
		using BinaryReader br = new(InputStream, Encoding.ASCII, true);

		_rootChunk = GameMakerChunk.CreateFromBinaryReader(br);
		_rootChunk.ParseAllChunks(br);
	}

	public IEnumerable<SubFile> GetSubFiles()
	{
		ParseInputStream();

		foreach (var chunk in _rootChunk.Subchunks)
		{
			switch (chunk.Name)
			{
				default:
					yield return new(chunk.Name, chunk.Offset, chunk.Length);
					break;
			}
		}
	}
}
