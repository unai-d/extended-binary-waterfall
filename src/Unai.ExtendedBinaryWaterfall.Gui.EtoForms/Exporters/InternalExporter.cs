using System.Collections.Generic;
using Eto.Drawing;
using Unai.ExtendedBinaryWaterfall.Exporters;
using Unai.ExtendedBinaryWaterfall.Renderers;

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

		public void PushNewFrame(ICanvas videoFrame, AudioBuffer audioFrame, double delta = 0.04)
		{
			var pixelData = new byte[videoFrame.Width * videoFrame.Height * 4];
			videoFrame.CopyPixelDataTo(pixelData);

			var bitmap = new Bitmap(videoFrame.Width, videoFrame.Height, PixelFormat.Format32bppRgba);

			using var bitmapData = bitmap.Lock();
			bitmapData.SetPixels(ConvertToEtoColor(pixelData));

			_mainForm._uiViewport.Image = bitmap;
		}

		private static IEnumerable<Color> ConvertToEtoColor(byte[] pixelData)
		{
			for (int i = 0; i < pixelData.Length; i += 4)
			{
				yield return Color.FromArgb(pixelData[i], pixelData[i + 1], pixelData[i + 2]);
			}
		}
	}
}
