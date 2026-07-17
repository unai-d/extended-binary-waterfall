using System;

namespace Unai.ExtendedBinaryWaterfall.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class ExporterAttribute(string id, string name = null, string description = null) : Attribute
{
	public string Id { get; set; } = id;
	public string Name { get; set; } = name;
	public string Description { get; set; } = description;

	public ExporterAttribute() : this(null) {  }
}
