using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace Unai.ExtendedBinaryWaterfall;

public static class FfmpegUtils
{
	private static readonly string[] _ffmpegSearchPathsLinux =
	[
		"/usr/lib",
		"/usr/lib64",
		"/usr/lib32",
		"/lib",
		"/lib64",
		"/lib32"
	];

	private static readonly string[] _ffmpegSearchPathsWindows =
	[
		"C:\\ffmpeg"
	];

	public static string GetFfmpegLibraryPath()
	{
		Logger.Debug("Guessing FFmpeg library path…");
		IEnumerable<string> ret;

		if (Environment.OSVersion.Platform == PlatformID.Win32NT)
		{
			ret = _ffmpegSearchPathsWindows
				.Where(Directory.Exists)
				.Where(x => Directory.GetFiles(x, "*avcodec-*.dll").Length > 0);
		}
		else
		{
			ret = _ffmpegSearchPathsLinux
				.Where(Directory.Exists)
				.Where(x => File.Exists($"{x}/libavcodec.so"));
		}
		
		Logger.Debug($"{ret.Count()} detected library paths.");
		if (ret.Any())
		{
			return ret.FirstOrDefault();
		}

		if (Environment.OSVersion.Platform == PlatformID.Win32NT)
		{
			Logger.Debug("Search via predefined paths failed. Trying PATH environment variable…");
			ret = Environment.GetEnvironmentVariable("PATH")
				.Split(';')
				.Where(Directory.Exists)
				.Where(p => Directory.GetFiles(p, "avcodec*.dll").Length > 0);

			if (ret.Any())
			{
				return ret.FirstOrDefault();
			}
		}

		Logger.Error("Cannot determine folder path containing FFmpeg libraries.");
		if (Environment.OSVersion.Platform == PlatformID.Win32NT)
		{
			Logger.Info("Please enter the following command to install FFmpeg libraries:");
			Logger.Info("	winget install \"FFmpeg (Shared)\"");
			Logger.Info("Once installed, restart the command line.");
			Logger.Info("Alternatively, you can download the FFmpeg libraries and save them to the following location:");
			Logger.Info($"	{_ffmpegSearchPathsWindows[0]}");
			Logger.Info("Note that these libraries may start with either `libav` or just `av` (e.g: `avcodec-61.dll`).");
		}
		return null;
	}

	public unsafe static void LogIfAvError(int errorCode, string message)
	{
		if (errorCode < 0)
		{
			byte* errbuf = (byte*)ffmpeg.av_malloc(1024);
			ffmpeg.av_make_error_string(errbuf, 1024, errorCode);
			Logger.Error($"FFmpeg error {errorCode}: {message}: {Marshal.PtrToStringUTF8((nint)errbuf)}");
			ffmpeg.av_free(errbuf);
		}
	}

	public static AVRational GetRational(int num, int den)
	{
		AVRational ret;
		ret.num = num;
		ret.den = den;
		return ret;
	}

	public unsafe static void LogFrameData(AVFrame* frame)
	{
		if (frame != null)
		{
			Logger.Trace($"frm: pts={frame->pts} dur={frame->duration} tb={frame->time_base.num}/{frame->time_base.den}");
		}
	}

	public unsafe static void LogPacketData(AVPacket* packet)
	{
		Logger.Trace($"pkt: str={packet->stream_index} pts={packet->pts} dts={packet->dts} dur={packet->duration} tb={packet->time_base.num}/{packet->time_base.den}");
	}
}
