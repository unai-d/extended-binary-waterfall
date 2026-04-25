using System;
using System.Collections.Generic;
using System.IO;

namespace Unai.ExtendedBinaryWaterfall.Parsers.GameMaker;

public class GameMakerChunk
{
	public string Name { get; set; } = null;
	public long Offset { get; internal set; }
	public uint Length { get; internal set; }
	public long PayloadOffset => Offset + 8;
	public virtual IEnumerable<GameMakerChunk> Subchunks { get; } = null;

	public static GameMakerChunk CreateFromBinaryReader(BinaryReader br)
	{
		var chunkOfs = br.BaseStream.Position;

		var chunkId = br.ReadString(4);
		uint chunkLen = br.ReadUInt32();

		Logger.Trace($"[{chunkOfs:X8}] chunk {chunkId} size {chunkLen}");

		// TODO: attributes can be used here.
		string chunkDotNetTypeName = typeof(GameMakerChunk).FullName + chunkId.ToUpper()[0] + chunkId.ToLower()[1..];
		GameMakerChunk chunk = (GameMakerChunk)Activator.CreateInstance(Type.GetType(chunkDotNetTypeName) ?? typeof(GameMakerChunk));
		chunk.Name = chunkId;
		chunk.Length = chunkLen;
		chunk.Offset = chunkOfs;

		br.BaseStream.Position += chunkLen;

		return chunk;
	}

	public virtual void ParseChunk(BinaryReader br)
	{
		var lastOffset = br.BaseStream.Position;

		br.BaseStream.Position = Offset;
		Name = br.ReadString(4);
		Length = br.ReadUInt32();

		br.BaseStream.Position = lastOffset;
	}

	public void ParseAllChunks(BinaryReader br)
	{
		ParseChunk(br);
		if (Subchunks != null)
		{
			foreach (var sc in Subchunks) sc.ParseChunk(br);
		}
	}
}
