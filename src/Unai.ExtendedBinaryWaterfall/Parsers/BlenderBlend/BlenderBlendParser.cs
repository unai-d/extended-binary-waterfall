using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Unai.ExtendedBinaryWaterfall.Parsers.BlenderBlend;

[Parser("blender", "Blender Project File", [".blend", ".blend1"])]
public class BlenderBlendParser : IParser
{
	public static byte[] HeaderMagic = [ 0x42, 0x4c, 0x45, 0x4e, 0x44, 0x45, 0x52 ];
	public static string GetFileBlockName(string fileBlockId)
	{
		return fileBlockId switch
		{
			"SC\0\0" => "Scene",
			"ME\0\0" => "Mesh",
			"WM\0\0" => "Window Manager",
			"DATA" => "Data Block",
			"ENDB" => "End of File Block",
			"DNA1" => "Structure DNA (Reflection Data)",
			"IM\0\0" => "Image",
			"GLOB" => "Global Data",
			"TEST" => "Thumbnail Data",
			"WS\0\0" => "Workspace",
			"SR\0\0" => "Screen Root",
			"BR\0\0" => "Brush",
			"OB\0\0" => "Object",
			"MA\0\0" => "Material",
			"NT\0\0" => "Shader Node Tree",
			"TX\0\0" => "Text",
			"GR\0\0" => "Collection",
			_ => $"File block `{fileBlockId}`"
		};
	}

	List<BlenderFileBlock> _fileBlocks = [];
	private List<string> _sdnaNames;
	private List<string> _sdnaTypes;
	private List<ushort> _sdnaTypeLen;
	private List<BlenderStructDef> _sdnaStructs;

	public Stream InputStream { get; set; }
	public Stream AuxiliaryInputStream { get; set; }

	public IEnumerable<SubFile> GetSubFiles()
	{
		using BinaryReader br = new(InputStream, Encoding.ASCII, true);

		var magicNum = br.ReadBytes(7);
		if (!Utils.ArrayCompare(magicNum, HeaderMagic)) Logger.Error("Invalid Blender project file signature.");
		var is64Bit = br.ReadByte() == '-';
		var isBigEndian = br.ReadByte() == 'V';
		if (isBigEndian) throw new NotSupportedException("Big endian Blender files not supported yet.");
		var verNum = br.ReadBytes(3);

		// HashSet<string> typesToResolve = [];

		while (br.BaseStream.Position < br.BaseStream.Length)
		{
			BlenderFileBlock fileBlock = new();

			fileBlock.Offset = br.BaseStream.Position;

			fileBlock.Id = Encoding.ASCII.GetString(br.ReadBytes(4));
			fileBlock.Size = br.ReadUInt32();
			fileBlock.OldMemAddress = is64Bit ? br.ReadUInt64() : br.ReadUInt32();
			fileBlock.SdnaIndex = br.ReadUInt32();
			fileBlock.SubStructureCount = br.ReadUInt32();

			Logger.Debug($"Read file block at 0x{fileBlock.Offset:x8}, id '{fileBlock.Id}', size '{fileBlock.Size}'.");

			br.BaseStream.Position += fileBlock.Size;

			_fileBlocks.Add(fileBlock);
		}

		var sdnaBlock = _fileBlocks.FirstOrDefault(fb => fb.Id == "DNA1");

		br.BaseStream.Position = sdnaBlock.Offset + (is64Bit ? 24 : 20);

		var identifier = Encoding.ASCII.GetString(br.ReadBytes(4));
		if (identifier != "SDNA") throw new InvalidDataException("Invalid SDNA marker.");

		var sdnaNameBlockId = Encoding.ASCII.GetString(br.ReadBytes(4));
		if (sdnaNameBlockId != "NAME") throw new InvalidDataException("Invalid SDNA DATA block ID.");
		_sdnaNames = [];
		var sdnaNameCount = br.ReadUInt32();
		for (int i = 0; i < sdnaNameCount; i++)
		{
			_sdnaNames.Add(br.ReadCString());
		}

		while (br.BaseStream.Position % 4 != sdnaBlock.Offset % 4) br.BaseStream.Position++;
		var sdnaTypeBlockId = Encoding.ASCII.GetString(br.ReadBytes(4));
		if (sdnaTypeBlockId != "TYPE") throw new InvalidDataException("Invalid SDNA TYPE block ID.");
		_sdnaTypes = [];
		var sdnaTypeCount = br.ReadUInt32();
		for (int i = 0; i < sdnaTypeCount; i++)
		{
			_sdnaTypes.Add(br.ReadCString());
		}

		while (br.BaseStream.Position % 4 != sdnaBlock.Offset % 4) br.BaseStream.Position++;
		var sdnaTypeLenBlockId = Encoding.ASCII.GetString(br.ReadBytes(4));
		if (sdnaTypeLenBlockId != "TLEN") throw new InvalidDataException("Invalid SDNA TLEN block ID.");
		_sdnaTypeLen = [];
		for (int i = 0; i < sdnaTypeCount; i++)
		{
			_sdnaTypeLen.Add(br.ReadUInt16());
		}

		while (br.BaseStream.Position % 4 != sdnaBlock.Offset % 4) br.BaseStream.Position++;
		var sdnaStructsBlockId = Encoding.ASCII.GetString(br.ReadBytes(4));
		if (sdnaStructsBlockId != "STRC") throw new InvalidDataException("Invalid SDNA STRC block ID.");
		_sdnaStructs = [];
		var sdnaStructsCount = br.ReadUInt32();
		for (int i = 0; i < sdnaStructsCount; i++)
		{
			BlenderStructDef structDef = new()
			{
				TypeIndex = br.ReadUInt16(),
				FieldCount = br.ReadUInt16(),
				Fields = []
			};

			for (int j = 0; j < structDef.FieldCount; j++)
			{
				var fieldTypeIndex = br.ReadUInt16();
				var fieldNameIndex = br.ReadUInt16();
				structDef.Fields.Add(new(fieldTypeIndex, fieldNameIndex));
			}

			_sdnaStructs.Add(structDef);
		}

		Logger.Debug($"Read {_sdnaStructs.Count} structs.");

		var idTypeIndex = _sdnaTypes.IndexOf("ID");
		var idStruct = _sdnaStructs.FirstOrDefault(strdef => strdef.TypeIndex == idTypeIndex);
		// var idStructNameField = idStruct.Fields.FirstOrDefault(flddef => flddef.Item2 == _sdnaNames.IndexOf("name"));
		var idStructNameFieldOfs = 0;
		var idStructNameFieldSize = 24;
		foreach (var idStructField in idStruct.Fields)
		{
			var fieldName = _sdnaNames[idStructField.Item2];
			var fieldTypeSize = _sdnaTypeLen[idStructField.Item1];

			if (fieldName.StartsWith("name["))
			{
				idStructNameFieldSize = GetFieldSize(is64Bit, fieldName, fieldTypeSize);
				break;
			}

			idStructNameFieldOfs += GetFieldSize(is64Bit, fieldName, fieldTypeSize);
		}

		foreach (var fileBlock in _fileBlocks)
		{
			string structInstanceName = null;

			var structDef = _sdnaStructs[(int)fileBlock.SdnaIndex];
			var structType = _sdnaTypes[structDef.TypeIndex];

			Logger.Debug($"File block with ID '{fileBlock.Id}' has {fileBlock.SubStructureCount} elements of type '{structType}'.");

			br.BaseStream.Position = fileBlock.Offset + (is64Bit ? 24 : 20);

			for (int i = 0; i < structDef.FieldCount; i++)
			{
				var fieldTypeIndex = structDef.Fields[i].Item1;
				var fieldNameIndex = structDef.Fields[i].Item2;

				var fieldType = _sdnaTypes[fieldTypeIndex];
				var fieldName = _sdnaNames[fieldNameIndex];
				var fieldTypeSize = _sdnaTypeLen[fieldTypeIndex];
				
				int fieldSize = GetFieldSize(is64Bit, fieldName, fieldTypeSize);

				// Logger.Debug($"Struct '{_sdnaTypes[structDef.TypeIndex]}': found field with type '{fieldType}', name '{fieldName}', size {fieldSize}.");

				if (fieldName.StartsWith("name["))
				{
					if (fieldType != "char") Logger.Warning($"Detected name field is not of type 'char'.");
					structInstanceName = Encoding.UTF8.GetString(br.ReadBytes(fieldSize)).TrimEnd('\0');
					break;
				}
				else if (fieldType == "ID" && fieldName == "id")
				{
					var idStructPos = br.BaseStream.Position;
					br.BaseStream.Position += idStructNameFieldOfs;
					structInstanceName = Encoding.UTF8.GetString(br.ReadBytes(idStructNameFieldSize)).TrimEnd('\0');
					br.BaseStream.Position = idStructPos + fieldSize;
					break;
				}
				else
				{
					br.BaseStream.Position += fieldSize;
				}
			}

			if (fileBlock.Id[2..] == "\0\0")
			{
				structInstanceName = structInstanceName?[2..];
			}

			SubFile ret = new(structInstanceName ?? fileBlock.Id, fileBlock.Offset, fileBlock.Size + (is64Bit ? 24 : 20))
			{
				Description = $"{GetFileBlockName(fileBlock.Id)}\nOffset: 0x{fileBlock.Offset:x8}"
			};

			if (fileBlock.Id != "ENDB" && fileBlock.Id != "DNA1")
			{
				ret.Description += $"\nData type: {structType}[{fileBlock.SubStructureCount}]";
			}

			yield return ret;
		}
	}

	private static int GetFieldSize(bool is64Bit, string fieldName, ushort fieldTypeSize)
	{
		int fieldSize = fieldTypeSize;

		if (fieldName[0] == '*')
		{
			fieldSize = is64Bit ? 8 : 4;
		}
		else if (fieldName.Contains('[') && !fieldName.Contains("][")) // TODO: Handle multidimensional arrays.
		{
			fieldSize *= int.Parse(fieldName.Substring(fieldName.IndexOf('[') + 1, fieldName.Length - 2 - fieldName.IndexOf('[')));
		}

		return fieldSize;
	}
}
