using System;

namespace Unai.ExtendedBinaryWaterfall.Parsers;

public class ParserAttribute(string id, string name = null, string[] extensions = null) : Attribute
{
	public string Id { get; set; } = id;
	public string Name { get; set; } = name;
	public string[] FileExtensions { get; set; } = extensions;

	public ParserAttribute() : this(null) {}
}
