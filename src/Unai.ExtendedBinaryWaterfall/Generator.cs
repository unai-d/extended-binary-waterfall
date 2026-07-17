using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unai.ExtendedBinaryWaterfall.Exporters;
using Unai.ExtendedBinaryWaterfall.Parsers;
using Unai.ExtendedBinaryWaterfall.Renderers;
using Unai.ExtendedBinaryWaterfall.Renderers.ImageSharp;

namespace Unai.ExtendedBinaryWaterfall;

public class Generator
{
	#region Main Fields

	public FileStream InputFileStream { get; set; }
	public Stream InputAuxiliaryFileStream { get; set; }
	public IRenderer Renderer { get; set; } = new ImageSharpRenderer();
	public IParser Parser { get; set; }
	public IExporter Exporter { get; set; }
	public long TotalFrames { get; internal set; }

	private readonly Stopwatch _timer = new();
	public List<SubFile> SubFiles { get; set; } = [];
	private BinaryReader _targetFileReader = null;
	private string _avSettingsString = null;
	private string _readSpeedString = null;

	public Dictionary<string, string> AdditionalCliArguments { get; } = [];

	#endregion

	#region Events

	public event Action OnFinish;
	public event Action<float> OnProgress;

	#endregion

	#region Generator State Machine Variables

	private ICanvas _frameContent = null;
	private ICanvas _viewportFramebuf = null;
	private AudioBuffer _inputAudioBuffer = null;
	private AudioBuffer _outputAudioBuffer = null;
	private int _videoFrameX1, _videoFrameX2, _videoFrameY1, _videoFrameY2;
	internal bool _exitRequested = false;
	private float _subfileWindowIndex = 0f;
	private IFont _font16, _font24, _font32, _font48;

	#endregion
	
	#region General Parameters

	public string InputFilePath { get; set; } = null;
	[CliParameter("Input File Listing File Path", "file-listing", "Set the file path that contains a text-based file listing if the input file format cannot be parsed entirely by this program")]
	public string InputAuxiliaryFilePath { get; set; } = null;
	[CliParameter("Output File Path", "output", 'o', "Set the output video file path")]
	public string OutputFilePath { get; set; } = null;
	[CliParameter("Title", "title", 't', "Set the title that will be shown during the binary waterfall describing the target file")]
	public string Title { get; set; } = null;
	[CliParameter("Author", "author", 'a', "Set the author of the generated binary waterfall")]
	public string Author { get; set; } = null;
	[CliParameter("Input File Parser", "parser", 'p', "Force a specific parser for the input file")]
	public string InputFileFormatId { get; set; } = null;
	[CliParameter("Exporter", "exporter", 'e', "Set the exporter to be used to export the generated binary waterfall")]
	public string ExporterId { get; set; } = null;
	[CliParameter("Input Bytes per Second", "input-bps", "Set the amount of bytes that will be read per audio/video second")]
	public int InputBytesPerSecond { get; set; } = 48000 * 2;
	[CliParameter("Font Name", "font", "Set the font name to render the on-screen text")]
	public string FontName { get; set; } = null;
	[CliParameter("Font Antialiasing", "font-antialiasing")]
	public bool FontAntialiasing { get; set; }

	#endregion

	public int InputBytesPerFrame => InputBytesPerSecond / OutputFps;

	#region Video Parameters

	[CliParameter("Output Video Width", "output-width")]
	public int OutputVideoWidth { get; set; } = 1920;
	[CliParameter("Output Video Height", "output-height")]
	public int OutputVideoHeight { get; set; } = 1080;
	[CliParameter("Output Framerate", "output-fps")]
	public int OutputFps { get; set; } = 60;
	public int WaterfallScaledWidth { get; set; } = 768;
	public int WaterfallScaledHeight { get; set; } = 768;
	[CliParameter("Input Video Width", "input-width")]
	public int WaterfallWidth { get; set; } = 256;
	[CliParameter("Input Video Height", "input-height")]
	public int WaterfallHeight { get; set; } = 256;
	public int WaterfallFrameLength => WaterfallWidth * WaterfallHeight * 4;

	#endregion

	#region Audio Parameters

	[CliParameter("Input Sample Format", "sample-format")]
	public AudioSampleFormat AudioInputSampleFormat { get; set; } = AudioSampleFormat.Unsigned8;
	[CliParameter("Input Audio Channel Count", "channel-count")]
	public int AudioInputChannelCount { get; set; } = 2;
	public int AudioInputSamplesPerFrame => InputBytesPerFrame / AudioInputSampleFormat.GetByteSize();
	public int AudioInputSamplesPerFramePerChannel => AudioInputSamplesPerFrame / AudioInputChannelCount;
	public int AudioInputSampleRate => (InputBytesPerSecond / AudioInputSampleFormat.GetByteSize()) / AudioInputChannelCount;
	public int AudioInputBytesPerFrame => InputBytesPerFrame;

	[CliParameter("Output Sample Format", "output-sample-format")]
	public AudioSampleFormat AudioOutputSampleFormat { get; set; } = AudioSampleFormat.Float32;
	[CliParameter("Output Audio Channel Count", "output-channel-count")]
	public int AudioOutputChannelCount { get; set; } = 2;
	[CliParameter("Output Sample Rate", "output-sample-rate")]
	public int AudioOutputSampleRate { get; set; } = 48000;
	public int AudioOutputSamplesPerFramePerChannel => AudioOutputSampleRate / OutputFps;
	public int AudioOutputSamplesPerFrame => AudioOutputSamplesPerFramePerChannel * AudioOutputChannelCount;
	public int AudioOutputBytesPerFrame => AudioOutputSampleFormat.GetByteSize() * AudioOutputSamplesPerFrame;

	#endregion

	#region Debug Flags

	public bool LogAllSubfiles { get; set; } = false;

	#endregion

	#region Initialization Methods

	public void Initialize()
	{
		Logger.Info("Initializing…");

		if (InputFileStream == null)
		{
			Logger.Info("Opening files…");
			Logger.Debug($"Opening file '{InputFilePath}'…");
			InputFileStream = File.OpenRead(InputFilePath);
			if (_targetFileReader != null)
			{
				_targetFileReader.Close();
				_targetFileReader = null;
			}
			_targetFileReader = new BinaryReader(InputFileStream, Encoding.Default, true);
		}
		
		TotalFrames = (InputFileStream.Length / InputBytesPerFrame) + 1;

		if (InputAuxiliaryFileStream == null)
		{
			if (InputAuxiliaryFilePath != null)
			{
				Logger.Debug($"Opening file '{InputAuxiliaryFilePath}'…");
				InputAuxiliaryFileStream = File.OpenRead(InputAuxiliaryFilePath);
				Logger.Debug($"  Done ({InputAuxiliaryFileStream.Length / 1024} KiB).");
			}
		}

		_avSettingsString = $"{AudioInputSampleRate} Hz, PCM {(AudioInputSampleFormat.IsSigned() ? "signed" : "unsigned")} {8 * AudioInputSampleFormat.GetByteSize()}-bit, {(AudioInputChannelCount == 2 ? "stereo" : "mono")}\nRGBA (32bpp), {WaterfallWidth} px/line";
		_readSpeedString = $"{InputBytesPerSecond / 1024} KiB/s";

		InitializeParser();

		ParseSubfiles();

		InitializeExporter();

		Logger.Info("Initializing renderer…");

		Renderer.Initialize();

		_font16 = Renderer.CreateFont("unifont", 16);
		_font24 = Renderer.CreateFont("unifont", 24);
		_font32 = Renderer.CreateFont("unifont", 32);
		_font48 = Renderer.CreateFont("unifont", 48);

		Logger.Info("Preparing audio/video generation…");

		UpdateLayout();

		_inputAudioBuffer = new(AudioInputSamplesPerFramePerChannel, AudioInputChannelCount);
		_outputAudioBuffer = new(AudioOutputSamplesPerFramePerChannel, AudioOutputChannelCount);
		// if (_drawOpts.GraphicsOptions.Antialias)
		// {
		// 	if (_drawOpts.GraphicsOptions.AntialiasSubpixelDepth < 0)
		// 	{
		// 		_drawOpts.GraphicsOptions.AntialiasSubpixelDepth = 1;
		// 	}
		// }

		LogGeneratorStatus();

		Logger.Info($"Initialization finished.");
	}

	[Conditional("DEBUG")]
	private void LogGeneratorStatus()
	{
		Logger.Debug($"Selected parser: {Parser?.GetType().GetCustomAttribute<ParserAttribute>()?.Name ?? Parser?.GetType().Name ?? "<null>"}");
		Logger.Debug($"Selected exporter: {Exporter?.GetType().GetCustomAttribute<ExporterAttribute>()?.Name ?? Exporter?.GetType().Name ?? "<null>"}");
		Logger.Debug($"Selected renderer: {Renderer?.GetType().GetCustomAttribute<RendererAttribute>()?.Name ?? Renderer?.GetType().Name ?? "<null>"}");
		Logger.Debug($"Read speed: {InputBytesPerFrame} bytes/frame ({InputBytesPerSecond} bytes/second)");
		Logger.Debug($"Waterfall duration will be around {TimeSpan.FromSeconds(InputFileStream.Length / InputBytesPerSecond)}.");
		Logger.Debug($"Video input:  {WaterfallWidth}×{WaterfallHeight}");
		Logger.Debug($"Audio input:  {AudioInputBytesPerFrame}bpf {AudioInputSamplesPerFrame}spf → {AudioInputSampleRate}Hz {AudioInputChannelCount}ch {8 * AudioInputSampleFormat.GetByteSize()}-bit");
		Logger.Debug($"Audio output: {AudioOutputBytesPerFrame}bpf {AudioOutputSamplesPerFrame}spf → {AudioOutputSampleRate}Hz {AudioOutputChannelCount}ch {8 * AudioOutputSampleFormat.GetByteSize()}-bit");
	}

	private void InitializeExporter()
	{
		if (Exporter != null)
		{
			Logger.Debug($"Exporter already set up: '{Exporter.GetType().Name}'.");
			return;
		}

		Logger.Info("Setting up exporter…");
		Logger.Debug($"Requested exporter: '{ExporterId}'.");

		if (ExporterId != null)
		{
			var availableExporters = Utils.GetTypesWithAttribute<ExporterAttribute>();
			foreach (var exporterKvp in availableExporters)
			{
				var exporterAttr = exporterKvp.Key;
				if (exporterAttr.Id != ExporterId)
				{
					continue;
				}
				Exporter = (IExporter)Activator.CreateInstance(exporterKvp.Value);
			}
			if (Exporter == null)
			{
				Logger.Fail($"Unknown exporter ID: '{ExporterId}'.");
				return;
			}
		}
		else
		{
			Logger.Debug("No exporter requested. Using SDL…");
			Exporter = new SdlExporter();
		}

		Exporter.Generator = this;

		if (AdditionalCliArguments.Count > 0)
		{
			Logger.Info($"Setting exporter properties from command line arguments…");
			foreach (var argKvp in AdditionalCliArguments)
			{
				var targetProp = Utils.GetPropertyFromCliArgument(argKvp.Key);

				if (targetProp.DeclaringType.GetInterfaces().Contains(typeof(IExporter)))
				{
					Logger.Debug($"Setting property '{argKvp.Key}' from exporter '{ExporterId ?? "<default>"}' to value '{argKvp.Value}'…");
					CliParameterAttribute.SetPropertyFromCliArgument(targetProp, Exporter, argKvp.Value);
				}
				else
				{
					Logger.Error($"Unrecognized CLI argument name: '{argKvp.Key}'.");
				}
			}
		}
	}

	private void ParseSubfiles()
	{
		if (SubFiles.Count != 0)
		{
			Logger.Debug("Subfiles already parsed.");
			return;
		}

		IEnumerable<SubFile> subFiles = null;

		if (Parser != null)
		{
			Logger.Info("Parsing subfiles…");

			// Pass the input stream(s) to the parser.
			Parser.InputStream = InputFileStream;
			Parser.AuxiliaryInputStream = InputAuxiliaryFileStream;

			// Ensure that input streams start at zero when starting the parsing process.
			InputFileStream.Position = 0;
			if (InputAuxiliaryFileStream != null) InputAuxiliaryFileStream.Position = 0;

			// Do the actual parsing.
			subFiles = Parser.GetSubFiles();

			// Order generated file listing by position inside the file (offset).
			// Parse further with `ParseSubfile` if necessary.
			SubFiles =
			[
				.. subFiles
				.OrderBy(sf => sf.StartOffset)
				.Select(sf => Utils.ParseSubfile(InputFileStream, sf))
			];
		}

		Logger.Debug($"Total number of subfiles: {SubFiles.Count}");

		if (LogAllSubfiles)
		{
			foreach (var sf in SubFiles)
			{
				Logger.Debug($"\t{sf.IconString ?? "–"} '{sf.Path}' {sf.StartOffset:X8}–{sf.EndOffset:X8}");
			}
		}
	}

	private void InitializeParser()
	{
		Logger.Info("Setting up parser…");
		var availableParsers = Utils.GetTypesWithAttribute<ParserAttribute>();

		if (InputFileFormatId != null)
		{
			Logger.Debug($"Requested parser: '{InputFileFormatId}'.");
			foreach (var parserKvp in availableParsers)
			{
				var parserAttr = parserKvp.Key;
				if (parserAttr.Id != InputFileFormatId)
				{
					continue;
				}
				Parser = (IParser)Activator.CreateInstance(parserKvp.Value);
			}
			if (Parser == null)
			{
				Logger.Warning($"Unknown parser ID: '{InputFileFormatId}'. Skipping subfile listing.");
			}
		}
		else
		{
			Logger.Info("Guessing input format from file extension…");
			var inputFileExt = Path.GetExtension(InputFilePath).ToLower();

			foreach (var parserKvp in availableParsers)
			{
				var parserAttr = parserKvp.Key;
				if (parserAttr.FileExtensions.Contains(inputFileExt))
				{
					Logger.Debug($"Parser '{parserAttr.Id}' recognizes '{inputFileExt}' as a valid file extension.");
					Parser = (IParser)Activator.CreateInstance(parserKvp.Value);
					break;
				}
			}
			if (Parser == null)
			{
				Logger.Warning($"Unknown input format. Skipping subfile listing.");
			}
		}
	}

	#endregion

	public void UpdateLayout(bool force = true)
	{
		if (force || _frameContent == null || _frameContent.Width != OutputVideoWidth || _frameContent.Height != OutputVideoHeight)
		{
			_frameContent = Renderer.CreateCanvas(OutputVideoWidth, OutputVideoHeight);
			var pixelCount = OutputVideoWidth * OutputVideoHeight;

			WaterfallScaledWidth = (int)(WaterfallWidth * (pixelCount / 691200f));
			WaterfallScaledHeight = (int)(WaterfallHeight * (pixelCount / 691200f));

			_videoFrameX1 = OutputVideoWidth / (SubFiles.Count > 0 ? 4 : 2) - WaterfallScaledWidth / 2;
			if (_videoFrameX2 == 0) _videoFrameX2 = _videoFrameX1 + WaterfallScaledWidth;
			_videoFrameY1 = OutputVideoHeight / 2 - WaterfallScaledHeight / 2;
			if (_videoFrameY2 == 0) _videoFrameY2 = _videoFrameY1 + WaterfallScaledHeight;
		}
	}

	public void Generate()
	{
		_timer.Start();

		// 1. Intro

		GenerateIntro();
		if (_exitRequested)
		{
			OnFinish?.Invoke();
			return;
		}

		// 2. Main Video

		GenerateMainVideo();

		OnFinish?.Invoke();
	}

	private void GenerateIntro()
	{
		Logger.Info("Generating introduction…");

		var totalIntroFrames = 5 * OutputFps; // 60FPS = 300

		for (long frameNumber = 0; frameNumber < totalIntroFrames; frameNumber++)
		{
			_frameContent
				.Clear(Color.FromArgb(16, 16, 16))
				.DrawText(_font48, new()
				{
					Origin = new(OutputVideoWidth / 2, OutputVideoHeight / 2),
					HorizontalAlignment = HorizontalAlignment.Center,
					TextAlignment = TextAlignment.Center,
				}, "DISCLAIMER\n\nThis video contains\nhigh speed flashing lights\nand loud noises", Color.White)
				.DrawText(_font24, new()
				{
					Origin = new(OutputVideoWidth / 2, OutputVideoHeight - 128),
					HorizontalAlignment = HorizontalAlignment.Center,
				}, $"Starting in {(totalIntroFrames - frameNumber) / (float)OutputFps:N1} seconds…", Color.White)
				.DrawProgressBar(frameNumber / (float)totalIntroFrames, (int)(OutputVideoWidth * 0.3), (int)(OutputVideoWidth * 0.7), OutputVideoHeight - 64);

			Exporter.PushNewFrame(_frameContent, _outputAudioBuffer, _timer.Elapsed.TotalSeconds);
			_timer.Restart();

			OnProgress?.Invoke(frameNumber / (float)totalIntroFrames);

			if (_exitRequested)
			{
				break;
			}
		}
	}

	private void GenerateMainVideo()
	{
		Logger.Info("Generating binary waterfall…");

		for (long currentFrame = 0; currentFrame < TotalFrames; currentFrame++)
		{
			try
			{
				GenerateFrame(currentFrame);
			}
			catch (Exception ex)
			{
				Logger.Error($"Uncaught exception when generating frame {currentFrame}: {ex}");
			}

			if (_exitRequested)
			{
				break;
			}
		}

		Exporter.Finish();
	}

	public void GenerateFrame(long frameNum = 0)
	{
		long currentOffset = frameNum * InputBytesPerFrame;

		// Get video buffer.

		int playHeadRelPos = 0;
		var frameStartByteOffset = currentOffset.Align(WaterfallWidth * 4) - (WaterfallFrameLength / 2);
		if (frameStartByteOffset < 0)
		{
			playHeadRelPos = (int)-(frameStartByteOffset / (WaterfallWidth * 4));
			frameStartByteOffset = 0;
		}
		else if (frameStartByteOffset + WaterfallFrameLength >= InputFileStream.Length)
		{
			playHeadRelPos = (int)((InputFileStream.Length - (frameStartByteOffset + WaterfallFrameLength)) / (WaterfallWidth * 4));
			frameStartByteOffset = InputFileStream.Length - WaterfallFrameLength;
		}
		var frameEndByteOffset = frameStartByteOffset + WaterfallFrameLength;

		InputFileStream.Position = frameStartByteOffset;
		var currentVideoBuffer = _targetFileReader.ReadBytes(WaterfallFrameLength);

		// Get audio buffer.

		var audioFrameStartByteOffset = currentOffset.Align(AudioInputSampleFormat.GetByteSize()) - (InputBytesPerFrame / 2);
		if (audioFrameStartByteOffset < 0)
		{
			audioFrameStartByteOffset = 0;
		}
		else if (audioFrameStartByteOffset + InputBytesPerFrame >= InputFileStream.Length)
		{
			audioFrameStartByteOffset = InputFileStream.Length - InputBytesPerFrame;
		}
		var audioFrameEndByteOffset = audioFrameStartByteOffset + InputBytesPerFrame;

		InputFileStream.Position = audioFrameStartByteOffset;
		var currentAudioBuffer = _targetFileReader.ReadBytes(InputBytesPerFrame);

		// Get video data.

		_viewportFramebuf = Renderer.CreateCanvas(currentVideoBuffer, WaterfallWidth, WaterfallHeight);
		// TODO: Remove alpha channel (make image opaque).
		// _viewportFramebuf.ProcessPixelRows(pa =>
		// {
		// 	for (int y = 0; y < pa.Height; y++)
		// 	{
		// 		var row = pa.GetRowSpan(y);
		// 		for (int x = 0; x < row.Length; x++)
		// 		{
		// 			row[x].A = 255;
		// 		}
		// 	}
		// });
		_viewportFramebuf.FlipVertical().Resize(WaterfallScaledWidth, WaterfallScaledHeight);

		// Get audio data.

		_inputAudioBuffer.LoadFromByteArray(currentAudioBuffer, AudioInputSampleFormat);
		_outputAudioBuffer = new AudioBuffer(_inputAudioBuffer)
			.Resample(AudioOutputSamplesPerFramePerChannel)
			.RemixChannels(AudioOutputChannelCount);

		// Compute registers.

		var subfilesInFrame = SubFiles
			.Select((sf, i) => new { key = i, value = sf })
			.Where(kvp => kvp.value.Intersects(currentOffset - (InputBytesPerFrame / 2), currentOffset + (InputBytesPerFrame / 2)))
			.ToList();
		var currentSubfile = subfilesInFrame.LastOrDefault();

		if (currentSubfile != null)
		{
			_subfileWindowIndex = .2f * _subfileWindowIndex + .8f * currentSubfile.key;
		}

		// Do render.

		//_frameContent.Mutate(ctx =>
		{
			// 1. Clear frame

			_frameContent.Clear(Color.FromArgb(16, 16, 16));

			// 2. Draw subfile listing

			int subfileX1 = OutputVideoWidth / 2;
			int subfileX2 = OutputVideoWidth - 32;

			int firstSubfileIndex = (int)(_subfileWindowIndex - 7);
			int lastSubfileIndex = (int)Math.Ceiling(_subfileWindowIndex + 7);

			float subfileH = 48;
			float subfileY = (OutputVideoHeight / 2) - (_subfileWindowIndex - firstSubfileIndex) * subfileH;

			for (int sfi = firstSubfileIndex; sfi <= lastSubfileIndex; sfi++)
			{
				int i = sfi - (currentSubfile?.key ?? 0);

				if (sfi < 0 || sfi >= SubFiles.Count)
				{
					subfileY += subfileH;
					continue;
				}

				var subfile = SubFiles[sfi];

				bool isMainSubfile = sfi == (currentSubfile?.key ?? -1);

				_frameContent
					.DrawText(_font32, new()
					{
						Origin = new(subfileX1, subfileY),
						VerticalAlignment = VerticalAlignment.Center,
					}, isMainSubfile ? "▶" : " ", Color.White)
					.DrawText(_font32, new()
					{
						Origin = new(subfileX1 + 32, subfileY),
						VerticalAlignment = VerticalAlignment.Center,
						// FallbackFontFamilies = _emojiFontFamily.Name != null ? [_emojiFontFamily] : null, // TODO
					}, $"{Utils.GetFileTypeEmoji(subfile)} {Utils.TruncateString(subfile.FileName, 40)}", Color.White)
					.DrawText(_font32, new()
					{
						Origin = new(subfileX2, subfileY),
						HorizontalAlignment = HorizontalAlignment.Right,
						VerticalAlignment = VerticalAlignment.Center,
					}, Utils.ToByteSizeString(subfile.Length), Color.DimGray);

				if (isMainSubfile)
				{
					float percentOfSubfile = (currentOffset - subfile.StartOffset) / (float)subfile.Length;

					_frameContent
						.DrawText(_font16, new()
						{
							Origin = new(subfileX1 + 48, subfileY + 20),
							HorizontalAlignment = HorizontalAlignment.Center,
							VerticalAlignment = VerticalAlignment.Center,
						}, $"{(int)Math.Clamp(percentOfSubfile * 100, 0, 100)} %", Color.White)
						.DrawProgressBar(percentOfSubfile, subfileX1 + 80, subfileX2, subfileY + 20);
				}

				subfileY += subfileH;
			}

			// 3. Draw binary waterfall viewport

			_frameContent
				.DrawImage(new PointF(_videoFrameX1, _videoFrameY1), _viewportFramebuf)
				.DrawText(_font32, new()
				{
					Origin = new(32, (OutputVideoHeight / 2) + (playHeadRelPos * (WaterfallScaledHeight / WaterfallHeight))),
					VerticalAlignment = VerticalAlignment.Center,
				}, "▶", Color.White);

			// 4. Draw top-bottom gradients

			float shadowY1 = (OutputVideoHeight / 2) - subfileH * 8.5f;
			float shadowY2 = (OutputVideoHeight / 2) + subfileH * 6.5f;

			_frameContent
				.FillRectangleGradient(
					new RectangleF(0, shadowY1, OutputVideoWidth, subfileH * 2),
					new PointF(0, shadowY1),
					new PointF(0, shadowY1 + subfileH * 2),
					new(0.5f, Color.FromArgb(255, 16, 16, 16)),
					new(1, Color.FromArgb(0, 16, 16, 16))
				)
				.FillRectangleGradient(
					new RectangleF(0, shadowY2, OutputVideoWidth, subfileH * 2),
					new PointF(0, shadowY2),
					new PointF(0, shadowY2 + subfileH * 2),
					new(0, Color.FromArgb(0, 16, 16, 16)),
					new(0.5f, Color.FromArgb(255, 16, 16, 16))
				)
				.DrawText(_font24, new()
				{
					Origin = new(subfileX1 + 40, 160),
					VerticalAlignment = VerticalAlignment.Center,
				}, Utils.TruncateString(currentSubfile?.value?.FileDirectory ?? string.Empty, 72), Color.DimGray);

			// 5. Draw Status and General Info

			_frameContent
				.DrawText(_font24, new()
				{
					Origin = new(32, 32),
				}, "A/V SETTINGS", Color.DimGray)
				.DrawText(_font32, new()
				{
					Origin = new(32, 32 + 24),
				}, _avSettingsString, Color.White)
				.DrawText(_font24, new()
				{
					Origin = new(OutputVideoWidth - 32, 32),
					HorizontalAlignment = HorizontalAlignment.Right,
				}, "ABS. OFFSET", Color.DimGray)
				.DrawText(_font32, new()
				{
					Origin = new(OutputVideoWidth - 32, 32 + 24),
					HorizontalAlignment = HorizontalAlignment.Right,
					TextAlignment = TextAlignment.Right,
				}, $"{currentOffset / 1048576f:N2} MiB\n0x{currentOffset:X8}", Color.White)
				.DrawText(_font24, new()
				{
					Origin = new(OutputVideoWidth - 256, 32),
					HorizontalAlignment = HorizontalAlignment.Right,
				}, "BITRATE", Color.DimGray)
				.DrawText(_font32, new()
				{
					Origin = new(OutputVideoWidth - 256, 32 + 24),
					HorizontalAlignment = HorizontalAlignment.Right,
				}, _readSpeedString, Color.White);

			if (Author != null)
			{
				_frameContent.DrawText(_font32, new()
				{
					Origin = new(OutputVideoWidth / 2, 32 + 24),
					VerticalAlignment = VerticalAlignment.Center,
					HorizontalAlignment = HorizontalAlignment.Center,
				}, Author, Color.White);
			}

			if (Title != null)
			{
				_frameContent
					.DrawText(_font24, new()
					{
						Origin = new(32, OutputVideoHeight - 64 - (Title.Contains('\n') ? 32 : 0)),
						VerticalAlignment = VerticalAlignment.Bottom,
					}, "TARGET", Color.DimGray)
					.DrawText(_font32, new()
					{
						Origin = new(32, OutputVideoHeight - 32),
						VerticalAlignment = VerticalAlignment.Bottom,
					}, Title, Color.White);
			}

			if (currentSubfile?.value?.Icon != null)
			{
				_frameContent.DrawImage(new Point(OutputVideoWidth / 2, OutputVideoHeight - 128 - 32), currentSubfile.value.Icon);
			}

			if (currentSubfile?.value?.Description != null)
			{
				_frameContent.DrawText(_font32, new()
				{
					Origin = new(OutputVideoWidth / 2 + 128 + 32, OutputVideoHeight - 32),
					VerticalAlignment = VerticalAlignment.Bottom,
				}, currentSubfile.value.Description, Color.White);
			}
		}

		Exporter.PushNewFrame(_frameContent, _outputAudioBuffer, _timer.Elapsed.TotalSeconds);
		_timer.Restart();

		currentOffset += InputBytesPerFrame;

		OnProgress?.Invoke(currentOffset / (float)InputFileStream.Length);
	}

	/// <summary>
	/// Intended to be called from a different thread, to stop the thread generating the video output to stop.
	/// </summary>
	public void StopGeneration()
	{
		_exitRequested = true;
	}
}