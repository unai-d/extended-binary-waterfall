using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Unai.ExtendedBinaryWaterfall.Parsers.ScratchProject;

[Parser("sb", "Scratch Project (1.x)", [ ".sb" ])]
public class ScratchProjectParser : IParser
{
	public Stream InputStream { get; set; }
	public Stream AuxiliaryInputStream { get; set; }

	List<ScratchProjectObject> Objects = [];

	public IEnumerable<SubFile> GetSubFiles()
	{
		using BinaryReader br = new(InputStream, Encoding.ASCII, true);

		var sbHeader = br.ReadBytes(10);
		var sbHeaderStr = Encoding.ASCII.GetString(sbHeader);
		if (sbHeaderStr != "ScratchV01" && sbHeaderStr != "ScratchV02")
		{
			Logger.Warning($"Unrecognized header magic: `{sbHeaderStr}`.");
		}
		var sbInfoSize = BinaryPrimitives.ReverseEndianness(br.ReadUInt32());

		yield return new("Header", 0, 10 + 4) { IconString = "🔶" };

		Logger.Debug("Reading `infoObjects` table…");
		var infoObjTab = ReadObjectTable(br).ToList();

		Logger.Debug("Reading `content` table…");
		Objects = ReadObjectTable(br).ToList();

		PrintObjectTable(Objects);

		Logger.Debug($"Stopped reading at 0x{br.BaseStream.Position:x8}/0x{br.BaseStream.Length:x8}.");

		foreach (var imageMedia in Objects.Where(o => o.Class == ScratchProjectObjectClass.ImageMedia))
		{
			Console.WriteLine(imageMedia.Value);

			var imageMediaFields = imageMedia.Value as Dictionary<string, ScratchProjectObject>;

			var imageMediaName = imageMediaFields.FirstOrDefault(f => f.Value.ResolveBaseClass(Objects) == ScratchProjectObjectClass.UTF8).Value;
			var imageMediaBitmap = imageMediaFields.FirstOrDefault(f => f.Value.ResolveBaseClass(Objects) == ScratchProjectObjectClass.Form || f.Value.ResolveBaseClass(Objects) == ScratchProjectObjectClass.ColorForm).Value;

			if (imageMediaBitmap == null) continue;

			imageMediaName = imageMediaName?.Resolve(Objects);
			imageMediaBitmap = imageMediaBitmap.Resolve(Objects);

			Console.WriteLine(imageMediaName);
			Console.WriteLine(imageMediaBitmap);

			var imageMediaForm = (ScratchForm)imageMediaBitmap.Value;
			var bitmapObject = Objects[imageMediaForm.PixelDataObjectIndex - 1];

			Console.WriteLine(bitmapObject);

			var bitmapSize = bitmapObject.Class != ScratchProjectObjectClass.ByteArray ? 0 : (int)bitmapObject.Value;

			yield return new($"{imageMediaName?.Value ?? $"Image media @ 0x{bitmapObject.Offset:x8}"}", bitmapObject.Offset, 1 + 4 + bitmapSize);
		}
	}

	private static void PrintObjectTable(IList<ScratchProjectObject> objects)
	{
		for (int i = 0; i < objects.Count; i++)
		{
			if (objects[i] == null) continue;
			Logger.Debug($"{i + 1,04} = {ResolveToString(objects[i], objects)}");

			if (objects[i].Value is List<ScratchProjectObject> subObjsList)
			{
				foreach (var subObj in subObjsList)
				{
					Logger.Debug($"    {ResolveToString(subObj, objects)}");
				}
			}
			else if (objects[i].Class == ScratchProjectObjectClass.Dictionary)
			{
				if (objects[i].Value is not Dictionary<ScratchProjectObject, ScratchProjectObject> dict) continue;

				foreach (var kvp in dict)
				{
					Logger.Debug($"    {ResolveToString(kvp.Key, objects)} = {ResolveToString(kvp.Value, objects)}");
				}
			}
			else if ((int)objects[i].Class >= 100)
			{
				if (objects[i].Value is not Dictionary<string, ScratchProjectObject> dict) continue;

				foreach (var kvp in dict)
				{
					Logger.Debug($"    {kvp.Key} = {ResolveToString(kvp.Value, objects)}");
				}
			}
		}
	}

	private static string ResolveToString(ScratchProjectObject obj, IList<ScratchProjectObject> objTab)
	{
		if (obj == null) return "<null>";
		if (obj.Class == ScratchProjectObjectClass.ObjectReference)
		{
			if ((int)obj.Value > objTab.Count) return "<out of bounds ref>";
			return $"{obj.Value} → {ResolveToString(objTab[(int)obj.Value - 1], objTab)}";
		}
		return obj.ToString();
	}

	IEnumerable<ScratchProjectObject> ReadObjectTable(BinaryReader br)
	{
		var otHeader = br.ReadBytes(10);
		var otObjCount = BinaryPrimitives.ReverseEndianness(br.ReadUInt32());

		Logger.Debug($"Reading object table with {otObjCount} objects…");

		for (int i = 0; i < otObjCount; i++)
		{
			ScratchProjectObject obj;

			try
			{
				obj = ReadObject(br);
			}
			catch (Exception ex)
			{
				Logger.Fail($"Uncaught exception when reading object #{i}: {ex}");
				continue;
			}

			yield return obj;
		}
	}

	ScratchProjectObject ReadObject(BinaryReader br)
	{
		if (br.BaseStream.Position == br.BaseStream.Length - 1) return null;

		byte objClassId = br.ReadByte();

		var objOfs = br.BaseStream.Position;
		var objClassIdEnum = (ScratchProjectObjectClass)objClassId;

		ScratchProjectObject ret = new(objClassIdEnum, objOfs);

		Logger.Debug($"Reading object at 0x{objOfs:x8}, class {objClassIdEnum}…");

		if (objClassId >= 1 && objClassId <= 8)
		{
			if (objClassIdEnum == ScratchProjectObjectClass.SmallInteger)
			{
				ret.Value = BinaryPrimitives.ReverseEndianness(br.ReadUInt32());
			}
			else if (objClassIdEnum == ScratchProjectObjectClass.SmallInteger16)
			{
				ret.Value = BinaryPrimitives.ReverseEndianness(br.ReadUInt16());
			}
			else if (objClassIdEnum == ScratchProjectObjectClass.LargePositiveInteger ||
				objClassIdEnum == ScratchProjectObjectClass.LargeNegativeInteger)
			{
				var length = BinaryPrimitives.ReverseEndianness(br.ReadUInt16());
				br.BaseStream.Position += length;
				ret.Value = long.MaxValue;
				Logger.Error($"Variable-sized integers not implemented yet!");
			}
			else if (objClassIdEnum == ScratchProjectObjectClass.Float)
			{
				ret.Value = BinaryPrimitives.ReadDoubleBigEndian(br.ReadBytes(8));
			}
		}
		else if (objClassId >= 9 && objClassId <= 19)
		{
			var objLength = BinaryPrimitives.ReverseEndianness(br.ReadUInt32());
			
			if (objClassIdEnum == ScratchProjectObjectClass.String || objClassIdEnum == ScratchProjectObjectClass.Symbol)
			{
				ret.Value = Encoding.ASCII.GetString(br.ReadBytes((int)objLength));
			}
			else if (objClassIdEnum == ScratchProjectObjectClass.SoundBuffer)
			{
				// TODO
				br.BaseStream.Position += objLength * 2;
			}
			else if (objClassIdEnum == ScratchProjectObjectClass.Bitmap)
			{
				// TODO
				br.BaseStream.Position += objLength * 4;
			}
			else if (objClassIdEnum == ScratchProjectObjectClass.UTF8)
			{
				ret.Value = Encoding.UTF8.GetString(br.ReadBytes((int)objLength));
			}
			else
			{
				ret.Value = (int)objLength;
				br.BaseStream.Position += objLength;
			}
		}
		else if (objClassId >= 20 && objClassId <= 29)
		{
			var objLength = BinaryPrimitives.ReverseEndianness(br.ReadUInt32());

			if (objClassIdEnum == ScratchProjectObjectClass.Array ||
				objClassIdEnum == ScratchProjectObjectClass.OrderedCollection ||
				objClassIdEnum == ScratchProjectObjectClass.Set ||
				objClassIdEnum == ScratchProjectObjectClass.IdentitySet)
			{
				var list = new List<ScratchProjectObject>();
				ret.Value = list;

				Logger.Debug($"→ Now inside `{objClassIdEnum}`, expecting {objLength} objects.");

				for (int i = 0; i < objLength; i++) list.Add(ReadObject(br));

				Logger.Debug($"← Now outside `{objClassIdEnum}`.");
			}
			else if (objClassIdEnum == ScratchProjectObjectClass.Dictionary ||
				objClassIdEnum == ScratchProjectObjectClass.IdentityDictionary)
			{
				var dict = new Dictionary<ScratchProjectObject, ScratchProjectObject>();
				ret.Value = dict;

				for (int i = 0; i < objLength; i++)
				{
					var key = ReadObject(br);
					var value = ReadObject(br);
					dict.Add(key, value);
				}
			}
		}
		else if (objClassIdEnum == ScratchProjectObjectClass.Color)
		{
			// TODO
			br.BaseStream.Position += 4;
		}
		else if (objClassIdEnum == ScratchProjectObjectClass.TranslucentColor)
		{
			// TODO
			br.BaseStream.Position += 5;
		}
		else if (objClassIdEnum == ScratchProjectObjectClass.Point)
		{
			// TODO
			ReadObject(br);
			ReadObject(br);
		}
		else if (objClassIdEnum == ScratchProjectObjectClass.Rectangle)
		{
			// TODO
			ReadObject(br);
			ReadObject(br);
			ReadObject(br);
			ReadObject(br);
		}
		else if (objClassIdEnum == ScratchProjectObjectClass.Form || objClassIdEnum == ScratchProjectObjectClass.ColorForm)
		{
			var widthF = ReadObject(br);
			var heightF = ReadObject(br);
			var depthF = ReadObject(br);
			ReadObject(br); // unknown
			var pixDataPtrF = ReadObject(br);
			if (objClassIdEnum == ScratchProjectObjectClass.ColorForm)
			{
				ReadObject(br); // color palette ptr
			}

			ret.Value = new ScratchForm()
			{
				Width = (ushort)widthF.Value,
				Height = (ushort)heightF.Value,
				Depth = (ushort)depthF.Value,
				PixelDataObjectIndex = (int)pixDataPtrF.Value,
			};

			// Logger.Debug($"Form data: {width}×{height} depth {depth}.");
		}
		else if (objClassIdEnum == ScratchProjectObjectClass.ObjectReference)
		{
			var indexBytes = br.ReadBytes(3);
			ret.Value = indexBytes[2] | indexBytes[1] << 8 | indexBytes[0] << 16;
		}
		else if (objClassId >= 100)
		{
			var version = br.ReadByte();
			var length = br.ReadByte();

			var dict = new Dictionary<string, ScratchProjectObject>();
			ret.Value = dict;

			var fieldNames = UserClassFields.GetFieldNamesByClassName(objClassIdEnum);

			Logger.Debug($"→ Now inside `{objClassIdEnum}`, expecting {length} objects.");

			for (int i = 0; i < length; i++)
			{
				string fieldName;
				if (fieldNames != null && i < fieldNames.Count)
				{
					fieldName = fieldNames[i];
				}
				else
				{
					fieldName = $"{i}";
				}
				
				dict.Add(fieldName, ReadObject(br));
			}

			Logger.Debug($"← Now outside `{objClassIdEnum}`.");
		}
		else
		{
			Logger.Error($"Class not implemented yet: `{objClassIdEnum}`.");
		}

		return ret;
	}
}
