using System;
using System.Collections.Generic;

namespace Unai.ExtendedBinaryWaterfall.Parsers.ScratchProject;

public class ScratchProjectObject
{
	public long Offset { get; set; }
	public ScratchProjectObjectClass Class { get; set; }
	public object Value { get; set; }

	public List<ScratchProjectObject> SubObjects { get; set; }
	public List<ScratchProjectObject> SubObjectsKeys { get; set; }

	public ScratchProjectObject(ScratchProjectObjectClass @class)
	{
		Class = @class;
	}

	public ScratchProjectObject(ScratchProjectObjectClass @class, long offset)
	{
		Class = @class;
		Offset = offset;
	}

	public override string ToString()
	{
		switch (Class)
		{
			case ScratchProjectObjectClass.Nil: return "<nil>";
			case ScratchProjectObjectClass.True: return "<true>";
			case ScratchProjectObjectClass.False: return "<false>";
			case ScratchProjectObjectClass.SmallInteger or ScratchProjectObjectClass.SmallInteger16: return Value.ToString();
			case ScratchProjectObjectClass.String or ScratchProjectObjectClass.Symbol or ScratchProjectObjectClass.UTF8: return (string)Value;
			case ScratchProjectObjectClass.Dictionary or ScratchProjectObjectClass.IdentityDictionary:
			{
				var dict = (IDictionary<ScratchProjectObject, ScratchProjectObject>)Value;
				return $"<dict, {dict.Count} items>";
			}
			case ScratchProjectObjectClass.Form:
			{
				var scratchForm = (ScratchForm)Value;
				return $"<form, {scratchForm.Width}×{scratchForm.Height}, depth {scratchForm.Depth}, pixels @ {scratchForm.PixelDataObjectIndex}>";
			}
			case ScratchProjectObjectClass.ByteArray: return $"<{(int)Value} bytes>";
			default: return $"`{Class}`={Value?.ToString()}";
		}
	}

	public ScratchProjectObjectClass ResolveBaseClass(IList<ScratchProjectObject> objTab)
	{
		if (Class == ScratchProjectObjectClass.ObjectReference && objTab != null)
		{
			return objTab[(int)Value - 1].ResolveBaseClass(objTab);
		}
		return Class;
	}

	public ScratchProjectObject Resolve(IList<ScratchProjectObject> objTab)
	{
		if (Class == ScratchProjectObjectClass.ObjectReference && objTab != null)
		{
			return objTab[(int)Value - 1].Resolve(objTab);
		}
		return this;
	}
}

public struct ScratchForm
{
	public int Width;
	public int Height;
	public int Depth;
	public int PixelDataObjectIndex;
	public int ColorPaletteObjectIndex;
}

public static class UserClassFields
{
	public static List<string> Morph = [ "bounds", "owner", "submorphs", "color", "flags", "properties"	];
	public static List<string> BorderedMorph = [ ..Morph, "borderWidth", "borderColor" ];
	public static List<string> AlignmentMorph = [ ..BorderedMorph, "orientation", "centering", "hResizing", "vResizing", "inset" ];
	public static List<string> StringMorph = [ ..Morph, "fontAndSize", "emphasis", "contents" ];
	public static List<string> ImageMorph = [ ..Morph, "form", "transparency" ];
	public static List<string> ScriptableScratchMorph = [ ..Morph, "objName", "vars", "blocksBin", "isClone", "media", "costume" ];
	public static List<string> ScratchSpriteMorph = [ ..ScriptableScratchMorph, "visibility", "scalePoint", "rotationDegrees", "rotationStyle", "volume", "tempoBPM", "draggable", "sceneStates", "lists" ];
	public static List<string> ScratchStageMorph = [ ..ScriptableScratchMorph, "zoom", "hPan", "vPan", "obsoleteSavedState", "sprites", "volume", "tempoBPM", "sceneStates", "lists" ];
	public static List<string> ScratchMedia = [ "mediaName" ];
	public static List<string> ImageMedia = [ ..ScratchMedia, "form", "rotationCenter", "textBox", "jpegBytes", "compositeForm" ];

	public static List<string> GetFieldNamesByClassName(ScratchProjectObjectClass @class)
	{
		return @class switch
		{
			ScratchProjectObjectClass.Morph => Morph,
			ScratchProjectObjectClass.BorderedMorph
				or ScratchProjectObjectClass.RectangleMorph
				or ScratchProjectObjectClass.EllipseMorph => BorderedMorph,
			ScratchProjectObjectClass.AlignmentMorph => AlignmentMorph,
			ScratchProjectObjectClass.StringMorph
				or ScratchProjectObjectClass.UpdatingStringMorph => StringMorph,
			ScratchProjectObjectClass.ImageMorph => ImageMorph,
			ScratchProjectObjectClass.ScriptableScratchMorph => ScriptableScratchMorph,
			ScratchProjectObjectClass.ScratchSpriteMorph => ScratchSpriteMorph,
			ScratchProjectObjectClass.ScratchStageMorph => ScratchStageMorph,
			ScratchProjectObjectClass.ScratchMedia => ScratchMedia,
			ScratchProjectObjectClass.ImageMedia => ImageMedia,
			_ => null,
		};
	}
}
