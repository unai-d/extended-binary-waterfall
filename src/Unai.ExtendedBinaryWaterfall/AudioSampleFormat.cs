namespace Unai.ExtendedBinaryWaterfall;

public enum AudioSampleFormat
{
	Unsigned8, Signed8,
	Unsigned16LE, Unsigned16BE, Signed16LE, Signed16BE,
	Unsigned32LE, Unsigned32BE, Signed32LE, Signed32BE,
	Float16, Float32, Float64,
}

public static class AudioSampleFormatExtensions
{
	public static int GetByteSize(this AudioSampleFormat asf)
	{
		return asf switch
		{
			AudioSampleFormat.Unsigned8 or AudioSampleFormat.Signed8 => 1,
			AudioSampleFormat.Unsigned16LE or AudioSampleFormat.Unsigned16BE or AudioSampleFormat.Signed16LE or AudioSampleFormat.Signed16BE or AudioSampleFormat.Float16 => 2,
			AudioSampleFormat.Unsigned32LE or AudioSampleFormat.Unsigned32BE or AudioSampleFormat.Signed32LE or AudioSampleFormat.Signed32BE or AudioSampleFormat.Float32 => 4,
			AudioSampleFormat.Float64 => 8,
			_ => 0
		};
	}

	public static bool IsSigned(this AudioSampleFormat asf)
	{
		return asf switch
		{
			AudioSampleFormat.Signed8 or AudioSampleFormat.Signed16LE or AudioSampleFormat.Signed16BE or AudioSampleFormat.Signed32LE or AudioSampleFormat.Float16 or AudioSampleFormat.Float32 or AudioSampleFormat.Float64 => true,
			_ => false,
		};
	}
}
