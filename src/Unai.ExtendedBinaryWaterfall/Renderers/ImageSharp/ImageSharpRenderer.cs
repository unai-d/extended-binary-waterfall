using System;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Unai.ExtendedBinaryWaterfall.Renderers.ImageSharp;

public class ImageSharpRenderer : IRenderer
{
	private FontCollection _fontCollection;
	private FontFamily _fontFamily;
	internal FontFamily _emojiFontFamily;

	public string FontName { get; set; }

	private void InitializeFonts()
	{
		Logger.Info("Loading fonts…");
		Logger.Debug($"Requested font: '{FontName}'.");

		if (_fontCollection == null)
		{
			_fontCollection = new();
			_fontCollection.AddSystemFonts();
		}

		if (FontName != null)
		{
			// Try getting the font by the font name specified by the user
			if (!_fontCollection.TryGet(FontName, out _fontFamily))
			{
				Logger.Error($"Cannot find font '{FontName}'.");
			}
		}

		if (_fontFamily.Name == null)
		{
			if (_fontCollection.TryGet("unifont", out _fontFamily))
			{
				// FIXME: This is hardcoded!
				_fontCollection.TryGet("unifont upper", out _emojiFontFamily);
			}
			else
			{
				_fontFamily = _fontCollection.Get(Environment.OSVersion.Platform == PlatformID.Win32NT ? "Consolas" : "Source Code Pro");
			}
		}
	}

	#region Interface Implementation

	public void Initialize()
	{
		InitializeFonts();
	}

	public ICanvas CreateCanvas(int width, int height)
	{
		return new ImageSharpCanvas(this, new Image<Rgba32>(width, height));
	}

	public ICanvas CreateCanvas(byte[] buf, int width, int height)
	{
		return new ImageSharpCanvas(this, Image.LoadPixelData<Rgba32>(buf, width, height));
	}

	public ICanvas CreateCanvasFromImage(byte[] imageData)
	{
		return new ImageSharpCanvas(this, Image.Load<Rgba32>(imageData));
	}

	public IFont CreateFont(string fontName, float size)
	{
		return new ImageSharpFont(_fontFamily.CreateFont(size, FontStyle.Regular));
	}

	#endregion
}
