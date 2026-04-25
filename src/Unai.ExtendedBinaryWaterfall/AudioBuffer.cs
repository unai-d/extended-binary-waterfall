using System;
using System.Buffers.Binary;
using System.Linq;

namespace Unai.ExtendedBinaryWaterfall;

public class AudioBuffer
{
	float[][] Samples { get; set; }
	public int ChannelCount => Samples.Length;
	public int SampleCount => Samples[0].Length;
	public int TotalSampleCount => SampleCount * ChannelCount;

	public AudioBuffer(int sampleCount, int channelCount)
	{
		Clear(sampleCount, channelCount);
	}

	public AudioBuffer(AudioBuffer source)
	{
		Clear(source.SampleCount, source.ChannelCount);
		for (int ch = 0; ch < ChannelCount; ch++)
		{
			Array.Copy(source.Samples[ch], Samples[ch], SampleCount);
		}
	}

	public AudioBuffer Clear(int? sampleCount = null, int? channelCount = null)
	{
		sampleCount ??= SampleCount;
		channelCount ??= ChannelCount;

		Samples = new float[channelCount.Value][];
		for (int ch = 0; ch < channelCount; ch++)
		{
			Samples[ch] = new float[sampleCount.Value];
		}

		return this;
	}

	public AudioBuffer LoadFromByteArray(byte[] buffer, AudioSampleFormat sampleFormat = AudioSampleFormat.Unsigned8)
	{
		float[] floatPcmBuffer = new float[buffer.Length / sampleFormat.GetByteSize()];

		switch (sampleFormat)
		{
			case AudioSampleFormat.Unsigned8:
				floatPcmBuffer = buffer.Select(x => (x / 128f) - 1f).ToArray();
				break;

			case AudioSampleFormat.Signed8:
				floatPcmBuffer = buffer.Select(x => x / 128f).ToArray();
				break;

			case AudioSampleFormat.Unsigned16LE:
				for (int i = 0; i < floatPcmBuffer.Length; i++)
				{
					var srcIdx = i * 2;
					var srcSample = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(srcIdx, 2));
					floatPcmBuffer[i] = (srcSample / 32768f) - 1f;
				}
				break;

			case AudioSampleFormat.Signed16LE:
				for (int i = 0; i < floatPcmBuffer.Length; i++)
				{
					var srcIdx = i * 2;
					var srcSample = BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(srcIdx, 2));
					floatPcmBuffer[i] = srcSample / 32768f;
				}
				break;

			default:
				throw new InvalidOperationException("Audio sample format not implemented yet.");
		}

		for (int i = 0; i < floatPcmBuffer.Length; i++)
		{
			if (i / ChannelCount >= Samples[0].Length)
			{
				Logger.Warning($"PCM sample at index {i} does not fit inside sample buffer at index {i / ChannelCount}.");
				return this;
			}
			Samples[i % ChannelCount][i / ChannelCount] = floatPcmBuffer[i];
		}

		return this;
	}

	public AudioBuffer Resample(int newSampleCount)
	{
		if (newSampleCount == SampleCount) return this;

		for (int ch = 0; ch < ChannelCount; ch++)
		{
			var newSampleBuffer = Samples[ch].LinearResample(newSampleCount);
			Samples[ch] = newSampleBuffer.ToArray();
		}

		return this;
	}

	public AudioBuffer RemixChannels(int newChannelCount)
	{
		if (newChannelCount == ChannelCount) return this;

		var newSamples = new float[newChannelCount][];

		for (int dch = 0; dch < newChannelCount; dch++)
		{
			var sch = (int)((dch / (float)newChannelCount) * ChannelCount);
			newSamples[dch] = new float[SampleCount];
			Array.Copy(newSamples[dch], Samples[sch], SampleCount);
		}

		Samples = newSamples;

		return this;
	}

	public float[] ToArray(bool planar = false)
	{
		float[] ret = new float[SampleCount * ChannelCount];

		if (planar)
		{
			throw new NotImplementedException();
		}

		for (int i = 0; i < ret.Length; i++)
		{
			ret[i] = Samples[i % ChannelCount][i / ChannelCount];
		}

		return ret;
	}
}