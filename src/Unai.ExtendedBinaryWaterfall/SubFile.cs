using Unai.ExtendedBinaryWaterfall.Renderers;

namespace Unai.ExtendedBinaryWaterfall;

public class SubFile(string path, long startOffset, long length)
{
	public string Path { get; set; } = path;
	public long StartOffset { get; set; } = startOffset;
	public long Length { get; set; } = length;
	public bool IsDirectory { get; set; } = false;
	public long EndOffset { get => StartOffset + Length; set => Length = value - StartOffset; }
	public string Description { get; set; } = null;
	public string IconString { get; set; } = null;
	public ICanvas Icon { get; set; } = null;

	public string FileName => System.IO.Path.GetFileName(Path);
	public string FileDirectory => System.IO.Path.GetDirectoryName(Path);
	public string Extension => System.IO.Path.GetExtension(Path);

	public bool Intersects(long start, long end) => end > StartOffset && start <= EndOffset;
}
