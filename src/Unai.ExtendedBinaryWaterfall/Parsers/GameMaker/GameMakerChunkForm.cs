using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Unai.ExtendedBinaryWaterfall.Parsers.GameMaker;

public class GameMakerChunkForm : GameMakerChunk
{
	private List<GameMakerChunk> _subchunks = null;
	public override IEnumerable<GameMakerChunk> Subchunks
	{
		get => _subchunks;
	}

	public IEnumerable<GameMakerChunk> GetSubchunks(BinaryReader br)
	{
		if (_subchunks != null)
		{
			foreach (var sc in _subchunks) yield return sc;
		}

		br.BaseStream.Position = PayloadOffset;

		while (br.BaseStream.Position < Offset + Length)
		{
			yield return GameMakerChunk.CreateFromBinaryReader(br);
		}
	}

	public override void ParseChunk(BinaryReader br)
	{
		base.ParseChunk(br);
		_subchunks = GetSubchunks(br).ToList();
	}
}
