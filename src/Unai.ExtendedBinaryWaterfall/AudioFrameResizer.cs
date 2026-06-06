using System;

namespace Unai.ExtendedBinaryWaterfall;

public class AudioFrameResizer<T>
{
	private T[] _buf = null;
	private int _triggerLen = 0;
	private int _bufPtr = 0;
	
	public int BufferLength
	{
		get => _buf?.Length ?? 0;
		set => _buf = new T[value];
	}
	public int TriggerLength
	{
		get => _triggerLen;
		set
		{
			ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, BufferLength, nameof(value));
			_triggerLen = value;
		}
	}
	public Action<T[]> OutputCallback { get; set; } = null;

	public void Push(T[] input)
	{
		var availSpace = BufferLength - _bufPtr;
		if (input.Length > availSpace)
		{
			throw new InvalidOperationException();
		}

		Logger.Trace($"Audio buffer status before insert: used {_bufPtr,4}, free {availSpace,4}, total {BufferLength,4}");

		Array.Copy(input, 0, _buf, _bufPtr, input.Length);
		_bufPtr += input.Length;

		Logger.Trace($"Audio buffer status after insert:  used {_bufPtr,4}, free {availSpace,4}, total {BufferLength,4}");
		
		while (_bufPtr >= TriggerLength)
		{
			OutputCallback?.Invoke(_buf[0..TriggerLength]);

			Array.Copy(_buf, TriggerLength, _buf, 0, BufferLength - TriggerLength);
			
			_bufPtr -= TriggerLength;
			
			Logger.Trace($"Audio buffer status after trigger: used {_bufPtr,-4}, free {availSpace,-4}, total {BufferLength,-4}");
		}
	}
}
