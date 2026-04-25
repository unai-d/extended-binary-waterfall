using System.Collections.Generic;
using Eto.Drawing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Unai.ExtendedBinaryWaterfall.Exporters;

namespace Unai.ExtendedBinaryWaterfall.Gui.EtoForms.Exporters
{
	public class InternalExporter : IExporter
	{
		private MainForm _mainForm;

		public Generator Generator { get; set; }

		public InternalExporter(MainForm mainForm)
		{
			_mainForm = mainForm;
			Generator = mainForm._generator;
		}

		public void Finish()
		{
			
		}

		public void PushNewFrame(SixLabors.ImageSharp.Image videoFrame, AudioBuffer audioFrame, double delta = 0.04)
		{
			if (videoFrame is Image<Rgba32> videoFrameRgba32)
			{
				PushNewFrame(videoFrameRgba32, audioFrame, delta);
			}
		}

		public void PushNewFrame(Image<Rgba32> videoFrame, AudioBuffer audioFrame, double delta = 0.04)
		{
			var pixelData = new byte[videoFrame.Width * videoFrame.Height * 4];
			videoFrame.CopyPixelDataTo(pixelData);

			var bitmap = new Bitmap(videoFrame.Width, videoFrame.Height, PixelFormat.Format32bppRgba);

			using var bitmapData = bitmap.Lock();
			bitmapData.SetPixels(ConvertToEtoColor(pixelData));

			_mainForm._uiViewport.Image = bitmap;
		}

		private static IEnumerable<Eto.Drawing.Color> ConvertToEtoColor(byte[] pixelData)
		{
			for (int i = 0; i < pixelData.Length; i += 4)
			{
				yield return Eto.Drawing.Color.FromArgb(pixelData[i], pixelData[i + 1], pixelData[i + 2]);
			}
		}
	}
}
