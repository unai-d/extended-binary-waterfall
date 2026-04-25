using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Unai.ExtendedBinaryWaterfall.Parsers.MiniDump;

class ModuleEntry(string fileName, ulong baseAddress, ulong size, ulong endAddress, uint timestamp)
{
	public string FileName = fileName;
	public ulong BaseAddress = baseAddress;
	public ulong Size = size;
	public ulong EndAddress = endAddress;
	public uint Timestamp = timestamp;
}

class MemoryEntry(ulong vaStart, ulong rva, ulong size)
{
	public ulong VAStart = vaStart;
	public ulong RVA = rva;
	public ulong Size = size;
}

[Parser("minidump", "Windows Memory Dump (Minidump)", [ ".dmp" ])]
public class MiniDumpParser : IParser
{
	public Stream InputStream { get; set; } = null;
	public Stream AuxiliaryInputStream { get; set; } = null;

	public IEnumerable<SubFile> GetSubFiles()
	{
		var ret = DoGetSubFiles();
		// Merge contiguous regions of memory with the same module file path.
		ret = [.. ret.MixLastOcurrences((sf, lsf) => sf.Path == lsf.Path, (lsf, sf) => { lsf.EndOffset = sf.EndOffset; })];
		// Prepend the minidump header.
		return
		[
			new("Header", 0, ret.OrderBy(sf => sf.StartOffset).FirstOrDefault().StartOffset) { IconString = "🔶" },
			.. ret,
		];
	}

	private IEnumerable<SubFile> DoGetSubFiles()
	{
		// var auxFilePath = ((FileStream)InputStream).Name + ".txt"; // FIXME: this is horrible.
		// if (!File.Exists(auxFilePath))
		// {
		// 	throw new FileNotFoundException($"File does not exist: '{auxFilePath}'.\nMake sure you generate a file with Python module 'minidump' (using the '--all' switch) that contains a list of executable modules, memory regions and other data at the specified path.\nExample:  python -m minidump --all [input_file] > [output_file]");
		// }
		// var auxFileStream = File.OpenRead(auxFilePath);

		List<ModuleEntry> modules = [];
		List<MemoryEntry> memoryRanges = [];
		char parseMode = ' ';
		bool doParse = false;

		using var sr = new StreamReader(AuxiliaryInputStream, Encoding.ASCII, leaveOpen: true);
		foreach (var line in sr.ReadToEnd().Split('\n'))
		{
			if (line.StartsWith("== "))
			{
				doParse = false;
				if (line == "== ModuleList ==")
				{
					parseMode = 'o';
				}
				else if (line == "== UnloadedModuleList ==")
				{
					parseMode = 'u';
				}
				else if (line == "== MinidumpMemory64List ==")
				{
					parseMode = '6';
				}
				else
				{
					parseMode = ' ';
				}
				continue;
			}

			if (!doParse && line.StartsWith("----"))
			{
				doParse = true;
				continue;
			}

			if (doParse && line.Contains(" | "))
			{
				var fields = line.Split(" | ").Select(s => s.Trim()).ToArray();
				switch (parseMode)
				{
					case 'o':
						modules.Add(new(
							fields[0],
							Utils.ParseHex(fields[1]),
							Utils.ParseHex(fields[2]),
							Utils.ParseHex(fields[3]),
							(uint)Utils.ParseHex(fields[4])
							));
						break;

					case 'u':
						modules.Add(new(
							fields[0],
							Utils.ParseHex(fields[1]),
							Utils.ParseHex(fields[2]),
							Utils.ParseHex(fields[3]),
							0
							));
						break;

					case '6':
						memoryRanges.Add(new(
							Utils.ParseHex(fields[0]),
							Utils.ParseHex(fields[1]),
							Utils.ParseHex(fields[2])
						));
						break;
				}
			}
		}

		foreach (var memoryRange in memoryRanges)
		{
			var ret = new SubFile("Unknown Memory", (long)memoryRange.RVA, (long)memoryRange.Size)
			{
				IconString = "❓",
				Description = $"Base Address: 0x{memoryRange.VAStart:X16}"
			};
			
			var module = modules.FirstOrDefault(m => Utils.Intersects(m.BaseAddress, m.EndAddress, memoryRange.VAStart, memoryRange.VAStart + memoryRange.Size));
			if (module != null)
			{
				ret.Path = module.FileName.Replace('\\', '/');
				ret.IconString = null;
			}
			yield return ret;
		}
	}
}
