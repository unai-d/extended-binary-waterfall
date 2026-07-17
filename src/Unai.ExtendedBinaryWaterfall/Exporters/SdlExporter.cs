using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SDL_Sharp;
using SDL_Sharp.Loader;
using Unai.ExtendedBinaryWaterfall.Attributes;
using Unai.ExtendedBinaryWaterfall.Renderers;

namespace Unai.ExtendedBinaryWaterfall.Exporters;

[Exporter("sdl", "SDL Window", "Show the generated audio and video data in a window.")]
public class SdlExporter : IExporter
{
	public Generator Generator { get; set; }

	private bool _init = false;
	private bool _isFullscreen = false;

	private Window _win;
	private Renderer _ren;
	private PSurface _surface;
	private uint _audioDeviceId;

	private byte[] _framebuffer = null;
	private readonly Stopwatch _sw = new();
	private int _frameCount = 0;
	private double _ts = 0;

	[CliParameter("Adaptive Output Video Resolution", "sdl:adaptive-video-resolution", "Change generator parameters based on window size")]
	public bool AdaptiveFramebufferSize { get; set; } = false;

	public void InitializeSdl()
	{
		SdlLoader.LoadDefault();
		unsafe
		{
			_ = SDL.Init(SdlInitFlags.Video | SdlInitFlags.Audio);
			_win = SDL.CreateWindow(
				"Extended Binary Waterfall",
				SDL.WINDOWPOS_UNDEFINED,
				SDL.WINDOWPOS_UNDEFINED,
				(int)(Generator.OutputVideoWidth * .75f),
				(int)(Generator.OutputVideoHeight * .75f),
				WindowFlags.Shown | WindowFlags.Resizable
			);
			if (_win.IsNull) throw new Exception("SDL cannot create a window.");
			_ren = SDL.CreateRenderer(_win, -1, RendererFlags.Accelerated);
			if (_ren.IsNull) throw new Exception("SDL cannot create a renderer.");
			SDL.CreateRGBSurface(0, Generator.OutputVideoWidth, Generator.OutputVideoHeight, 32, 0xff, 0xff00, 0xff0000, 0, out _surface);
			if (_surface.IsNull) throw new Exception("SDL cannot create a surface.");
			_framebuffer = new byte[Generator.OutputVideoWidth * Generator.OutputVideoHeight * 4];

			AudioSpec audioSpec;
			audioSpec.Frequency = Generator.AudioOutputSampleRate;
			audioSpec.Format = 0x8120; // 32-bit float LE
			audioSpec.Channels = 2;
			_audioDeviceId = SDL.OpenAudioDevice(null, 0, &audioSpec, null, 0);
			Logger.Debug($"OpenAudioDevice() = {_audioDeviceId}");
			_ = SDL.PauseAudioDevice(_audioDeviceId, false);
		}
		_sw.Start();
		_init = true;
	}

	public void PushNewFrame(ICanvas videoFrame, AudioBuffer audioFrame, double delta)
	{
		if (!_init)
		{
			InitializeSdl();
		}

		// Handle Video

		unsafe
		{
			SDL.RenderClear(_ren);

			videoFrame.CopyPixelDataTo(_framebuffer);
			SDL.SetRenderDrawColor(_ren, 0, 32, 0, 255);
			Marshal.Copy(_framebuffer, 0, (nint)((Surface*)_surface)->Pixels, _framebuffer.Length);
			
			var tex = SDL.CreateTextureFromSurface(_ren, _surface);
			if (tex.IsNull)
			{
				Logger.Error("Texture is null!");
			}
			SDL.RenderCopy(_ren, tex, 0, 0);

			SDL.RenderPresent(_ren);

			SDL.DestroyTexture(tex);
		}

		_frameCount++;
		_ts = _sw.Elapsed.TotalSeconds;
		var wcFrameCount = (int)(_ts * Generator.OutputFps);
		var framediff = _frameCount - wcFrameCount; // positive = too fast
		var deltaFps = 1 / delta;
		var renderSpeedRatio = deltaFps / Generator.OutputFps;

		if (framediff > 1)
		{
			SDL.Delay((uint)(((1 / (float)Generator.OutputFps) - delta) * 1000));
		}

		// Handle Audio

		var audioQueue = SDL.GetQueuedAudioSize(_audioDeviceId);

		if (audioQueue < audioFrame.TotalSampleCount * 4)
		{
			unsafe
			{
				fixed (float* audioBufPtr = audioFrame.ToArray())
				{
					var ret = SDL.QueueAudio(_audioDeviceId, (byte*)audioBufPtr, audioFrame.TotalSampleCount * sizeof(float));
					if (ret != 0) Logger.Error($"Cannot queue audio buffer (code {ret}): {SDL.GetError()}");
				}
			}
		}

		// Handle Window Events

		while (SDL.PollEvent(out Event e) == 1)
		{
			switch (e.Type)
			{
				case EventType.Quit:
					Generator._exitRequested = true;
					return;

				case EventType.KeyDown:
					var scancode = e.Keyboard.Keysym.Scancode;
					switch (scancode)
					{
						case Scancode.Escape:
							Generator._exitRequested = true;
							return;

						case Scancode.F11:
							_ = SDL.SetWindowFullscreen(_win, _isFullscreen ? 0 : WindowFlags.Fullscreen);
							_isFullscreen = !_isFullscreen;
							break;
					}
					break;

				case EventType.WindowEvent:
					SDL.GetWindowSize(_win, out int width, out int height);
					if (AdaptiveFramebufferSize)
					{
						if (width != Generator.OutputVideoWidth || height != Generator.OutputVideoHeight)
						{
							Generator.OutputVideoWidth = width;
							Generator.OutputVideoHeight = height;
							_framebuffer = new byte[Generator.OutputVideoWidth * Generator.OutputVideoHeight * 4];
							SDL.FreeSurface(_surface);
							SDL.CreateRGBSurface(0, Generator.OutputVideoWidth, Generator.OutputVideoHeight, 32, 0xff, 0xff00, 0xff0000, 0, out _surface);
							Generator.UpdateLayout();
						}
					}
					break;
			}
		}

		if (renderSpeedRatio < 1)
		{
			Logger.Warning($"Render too slow! Generator is rendering at {renderSpeedRatio:N2}× speed.");
		}

		Console.Error.Write($"frame={_frameCount,6} wcframe={wcFrameCount,6} diff={framediff,6} — {(int)deltaFps} fps aqueue={audioQueue}\x1b[K\x1b[G");
	}

	public void Finish()
	{
		_ = SDL.CloseAudioDevice(_audioDeviceId);
		SDL.FreeSurface(_surface);
		SDL.DestroyRenderer(_ren);
		SDL.DestroyWindow(_win);
		SDL.Quit();
	}
}
