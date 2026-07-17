using System;

namespace Unai.ExtendedBinaryWaterfall.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class RendererAttribute(string id, string name = null, string description = null) : Attribute
{
	public string Id { get; set; } = id;
	public string Name { get; set; } = name;
	public string Description { get; set; } = description;

	public RendererAttribute() : this(null) {  }
}
