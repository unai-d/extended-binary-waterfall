using System.Collections.Generic;
using System.Runtime.InteropServices;
using Raylib_cs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Unai.ExtendedBinaryWaterfall.Exporters;

namespace Unai.ExtendedBinaryWaterfall.Gui.RlImGui.Exporters
{
	public class InternalExporter : IExporter
	{
		public Generator Generator { get; set; }

		public InternalExporter(Generator generator)
		{
			Generator = generator;
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

			ref var image = ref Program._uiViewportPanel._image;

			unsafe
			{
				bool reallocRequested = false;

				if (image.Width != videoFrame.Width)
				{
					reallocRequested = true;
					image.Width = videoFrame.Width;
				}
				if (image.Height != videoFrame.Height)
				{
					reallocRequested = true;
					image.Height = videoFrame.Height;
				}

				image.Format = PixelFormat.UncompressedR8G8B8A8;
				image.Mipmaps = 1;

				if ((nint)image.Data == 0)
				{
					image.Data = (void*)Marshal.AllocHGlobal(pixelData.Length);
				}
				else if (reallocRequested)
				{
					image.Data = (void*)Marshal.ReAllocHGlobal((nint)image.Data, pixelData.Length);
				}

				Marshal.Copy(pixelData, 0, (nint)image.Data, pixelData.Length);

				if (reallocRequested)
				{
					Logger.Debug($"Updated Raylib image of current frame: {image.Width}×{image.Height} {image.Format}, {image.Mipmaps} mipmaps, data 0x{(nint)image.Data:x12}");
				}
			}

			Program._uiViewportPanel._imageHasChanged = true;
		}
	}
}
