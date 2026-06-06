using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Unai.ExtendedBinaryWaterfall.Parsers.WindowsIcon;

namespace Unai.ExtendedBinaryWaterfall;

public static class Utils
{
	public static string GetFileTypeEmoji(SubFile subFile)
	{
		if (subFile.IconString != null) return subFile.IconString;
		if (subFile.IsDirectory) return "🗀";
		return GetFileTypeEmojiFromExtension(subFile.Extension);
	}

	public static string GetFileTypeEmojiFromExtension(string extension)
	{
		if (extension == null) return null;

		return extension.ToLower() switch
		{
			".png" or
			".jpg" or ".jpeg" or ".jpe" or
			".bmp" or ".dib" or
			".ico" or
			".gif" or
			".heic" or
			".webp" or
			".dng" or
			".tif" or ".tiff"
				=> "🖼",
			".sys" or
			".dll" or
			".cpl" or
			".msc" or
			".ax"
				=> "⚙️",
			".txt" or
			".ini" or
			".inf" or
			".htm" or ".html" or
			".xml" or
			".sql" or
			".log" or
			".json"
				=> "🖹",
			".wav" or
			".mp3" or ".mp2" or ".mp1" or
			".wma" or
			".mid" or ".midi"
				=> "🎵",
			".avi" or
			".wmv" or
			".mp4" or
			".3gp" or ".3gpp" or
			".mov" or
			".mkv" or
			".mpg" or ".mpeg" or ".vob" or
			".ogv"
				=> "🎞️",
			".ufont" or
			".ttf" or
			".ttc" or
			".fon"
				=> "🗛",
			".chm" or
			".epub"
				=> "🕮",
			".bat" or
			".exe" or
			".com" or
			".scr"
				=> "🗔",
			".cab" or
			".7z" or
			".rar" or
			".tar" or
			".tar.gz" or
			".zip"
				=> "📦️",
			".cur" or
			".ani"
				=> "🖰",
			_ => "🗋",
		};
	}

	public static string ToByteSizeString(long value)
	{
		if (value < 1024)
		{
			return $"{value:N0} B";
		}

		float fvalue = value / 1024f;

		string[] suffixes = [ "KiB", "MiB", "GiB" ];

		for (int i = 0; i < suffixes.Length; i++)
		{
			if (fvalue < 1024)
			{
				return $"{fvalue:N1} {suffixes[i]}";
			}
			fvalue /= 1024f;
		}

		return $"Infinity";
	}

	public static string TruncateString(string input, int maxLength)
	{
		if (input == null) return null;
		return input.Length > maxLength ? input[..(maxLength - 1)] + "…" : input;
	}

	public static ulong ParseHex(string input)
	{
		return ulong.Parse(input[2..], System.Globalization.NumberStyles.HexNumber);
	}

	public static bool Intersects(ulong aStart, ulong aEnd, ulong bStart, ulong bEnd)
	{
		return Math.Max(aStart, bStart) <= Math.Min(aEnd, bEnd);
	}

	public static IEnumerable<T> SkipLastRepetition<T>(this IEnumerable<T> input, Func<T, T, bool> criteria) where T : class
	{
		T last = null;
		foreach (var item in input)
		{
			if (last == null) 
			{
				yield return item;
				last = item;
			}
			else if (criteria(item, last))
			{
				yield return item;
				last = item;
			}
		}
	}

	public static IEnumerable<T> MixLastOcurrences<T>(this IEnumerable<T> input, Func<T, T, bool> criteria, Action<T, T> mixFunc) where T : class
	{
		bool first = true;
		T output = null;
		foreach (var item in input)
		{
			if (first)
			{
				output = item;
				first = false;
			}
			else if (criteria(item, output))
			{
				mixFunc(output, item);
			}
			else
			{
				yield return output;
				output = item;
			}
		}
		yield return output;
	}

	internal static SubFile ParseSubfile(Stream target, SubFile sf)
	{
		var ext = sf.Extension?.ToLower();

		switch (ext)
		{
			case ".exe" or ".dll" or ".sys" or ".scr" or ".ocx" or ".ax" or ".cpl" or ".mui":
				Logger.Debug($"Parsing PE executable from subfile '{sf.Path}'…");
				try
				{
					target.Position = sf.StartOffset;
					byte[] peFileBuf = new byte[sf.Length];
					target.ReadExactly(peFileBuf);
					var peFile = new PeNet.PeFile(peFileBuf);

					if (peFile.Resources != null)
					{
						foreach (var stringEntry in peFile.Resources.VsVersionInfo.StringFileInfo.StringTable)
						{
							sf.Description = $"{stringEntry.OriginalFilename}\n{stringEntry.ProductName}\n{stringEntry.ProductVersion}\n{stringEntry.FileDescription}";
						}
						if (peFile.Resources.GroupIconDirectories != null)
						{
							try
							{
								foreach (var giDir in peFile.Resources.GroupIconDirectories)
								{
									foreach (var bestIconGi in giDir.DirectoryEntries.OrderByDescending(gi => gi.WBitCount).OrderByDescending(gi => gi.BWidth))
									{
										foreach (var bestIcon in bestIconGi.AssociatedIcons(peFile))
										{
											byte[] iconData = bestIcon.AsIco(); // returns either headless BMP or PNG
											sf.Icon = GetImageFromWindowsIconData(iconData);

											if (sf.Icon != null) break;
										}

										if (sf.Icon != null) break;
									}

									if (sf.Icon != null) break;
								}
							}
							catch (Exception ex)
							{
								Logger.Error($"Cannot set subfile icon from an icon group: {ex.Message}");
							}
						}
					}

					if (sf.Icon == null)
					{
						foreach (var icon in peFile.Icons())
						{
							sf.Icon = GetImageFromWindowsIconData(icon);
											
							if (sf.Icon != null) break;
						}
					}
				}
				catch (Exception ex)
				{
					Logger.Error($"Cannot parse PE executable: {ex.Message}");
				}
				break;
			
			case ".bmp" or ".jpg" or ".jpeg" or ".png" or ".tif" or ".tiff" or ".png" or ".webp" or ".tga":
				try
				{
					target.Position = sf.StartOffset;
					byte[] imageBuf = new byte[sf.Length];
					target.ReadExactly(imageBuf);
					sf.Icon = Image.Load(imageBuf);
				}
				catch (Exception ex)
				{
					Logger.Error($"Cannot read image subfile: {ex.Message}");
				}
				break;
		}
					
		sf.Icon?.Mutate(ctx => ctx.Resize(0, 128));

		return sf;
	}

	private static Image GetImageFromWindowsIconData(byte[] iconData)
	{
		ArgumentNullException.ThrowIfNull(iconData);
		
		if (iconData[0] == 0x89 && iconData[1] == 0x50) // PNG
		{
			return Image.Load(iconData);
		}
		
		var iconParser = new WindowsIconParser();
		iconParser.Load(iconData);

		return Image.Load(iconParser.Entries.First().GetBitmap());
	}

	internal static IEnumerable<float> NearestNeighborResample(this IList<float> input, int newSampleCount = 48000)
	{
		for (int i = 0; i < newSampleCount; i++)
		{
			double ratio = i / (double)newSampleCount;
			int srcIndex = (int)(ratio * input.Count);
			yield return input[srcIndex];
		}
	}

	public static IEnumerable<float> LinearResample(this IList<float> input, int newSampleCount = 48000)
	{
		for (int i = 0; i < newSampleCount; i++)
		{
			double ratio = i / (double)newSampleCount;
			float srcIndex = (float)(ratio * input.Count); // e.g.: 4.75
			int srcIndexInt = (int)srcIndex; // 4
			float srcIndexDec = srcIndex - srcIndexInt; // 0.75
			yield return ((1 - srcIndexDec) * input[srcIndexInt]) + (srcIndexDec * input[(srcIndexInt + 1) % input.Count]);
		}
	}

	public static IEnumerable<float>[] ToPlanar(this IEnumerable<float> input, int channelCount = 2)
	{
		var ret = new IEnumerable<float>[channelCount];
		for (int i = 0; i < channelCount; i++)
		{
			int currentChannel = i;
			ret[i] = input.Where((_, sampleIndex) =>
			{
				return sampleIndex % channelCount == currentChannel;
			});
		}
		return ret;
	}

	public static IEnumerable<float> ToPacked(this IList<float>[] input)
	{
		var channelCount = input.Length;
		for (int i = 0; i < input[0].Count; i++)
		{
			for (int ch = 0; ch < channelCount; ch++) yield return input[ch][i];
		}
	}

	public static int Align(this int input, int boundary)
	{
		return (input / boundary) * boundary;
	}

	public static long Align(this long input, int boundary)
	{
		return (input / boundary) * boundary;
	}

	public static float Align(this float input, int boundary)
	{
		return (int)(input / boundary) * boundary;
	}

	public static string GetBufferHexString(BinaryReader br, int count = 4)
	{
		var ofs = br.BaseStream.Position;

		var buf = br.ReadBytes(count);
		var bufHex = string.Join(' ', buf.Select(x => x.ToString("X2")));
		var bufAscii = string.Join("", buf.Select(x => char.IsBetween((char)x, ' ', '\x7f') ? (char)x : '.'));
		string ret = $"[{br.BaseStream.Position:X12}] {bufHex} {bufAscii}";

		br.BaseStream.Position = ofs;

		return ret;
	}

	public static IDictionary<T, Type> GetTypesWithAttribute<T>(Assembly asm = null) where T : Attribute, new()
	{
		asm ??= Assembly.GetExecutingAssembly();
		return asm.GetExportedTypes()
			.Where(t => t.GetCustomAttribute<T>() != null)
			.ToDictionary(t => t.GetCustomAttribute<T>());
	}

	public static IEnumerable<PropertyInfo> GetPropertiesWithAttribute<T>(Assembly asm = null) where T : Attribute, new()
	{
		asm ??= Assembly.GetExecutingAssembly();
		foreach (var type in asm.GetExportedTypes())
		{
			foreach (var prop in type.GetProperties().Where(p => p.GetCustomAttribute<T>() != null))
			{
				yield return prop;
			}
		}
	}

	public static IEnumerable<PropertyInfo> GetPropertiesWithAttribute<T>(Type t) where T : Attribute, new()
	{
		return t.GetProperties().Where(p => p.GetCustomAttribute<T>() != null);
	}

	public static PropertyInfo GetPropertyFromCliArgument(string argName)
	{
		return GetPropertiesWithAttribute<CliParameterAttribute>()
			.Where(p => argName.Length == 2 ? p.GetCustomAttribute<CliParameterAttribute>().ShortParameterName == argName[1] : p.GetCustomAttribute<CliParameterAttribute>().LongParameterName == argName[2..]).FirstOrDefault();
	}

	public static bool ArrayCompare<T>(T[] a1, T[] a2) where T : IComparable<T>
	{
		ArgumentNullException.ThrowIfNull(a1);
		ArgumentNullException.ThrowIfNull(a2);
		if (a1.Length != a2.Length) throw new ArgumentException("Array size mismatch.");

		for (int i = 0; i < a1.Length; i++)
		{
			if (a1[i].CompareTo(a2[i]) != 0) return false;
		}

		return true;
	}
}
