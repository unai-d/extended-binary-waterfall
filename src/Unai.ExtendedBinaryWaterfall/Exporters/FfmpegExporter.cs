using System;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Unai.ExtendedBinaryWaterfall.Exporters;

[Exporter("ffmpeg", "FFmpeg Stream", "Use FFmpeg libraries to encode audio and video data and output it in Matroska format.")]
public class FfmpegExporter : IExporter
{
	private bool _init = false;

	private unsafe AVFormatContext* _fmtCtx;

	private unsafe AVStream* _videoStream;
	private unsafe AVCodecContext* _videoCtx;
	private unsafe AVFrame* _videoAvFrame;
	private unsafe AVFrame* _videoAvFramePre;
	private unsafe AVPacket* _videoAvPacket;

	private unsafe AVStream* _audioStream;
	private unsafe AVCodecContext* _audioCtx;
	private unsafe AVFrame* _audioAvFrame;
	private unsafe AVPacket* _audioAvPacket;

	private unsafe SwsContext* _swsCtx;

	private readonly AudioFrameResizer<float> _audioQueue = new();

	private int _frameNum = 0;

	public Generator Generator { get; set; }
	
	#region User-defined properties

	[CliParameter("FFmpeg Log Level", "ffloglevel")]
	public int LogLevel { get; set; } = ffmpeg.AV_LOG_INFO;
	// [CliParameter("Output Video File Path", "output", 'o')]
	// public string OutputPath { get; set; } = null;
	[CliParameter("Output Video Bitrate", "output-bitrate")]
	public uint OutputVideoBitRate { get; set; } = 9_000_000;

	#endregion

	public void InitializeFfmpeg()
	{
		unsafe
		{
			ffmpeg.RootPath = FfmpegUtils.GetFfmpegLibraryPath();
			Logger.Debug($"FFmpeg library path: '{ffmpeg.RootPath}'.");
			
			ffmpeg.av_log_set_level(LogLevel);
			av_log_set_callback_callback logCb = (p0, level, format, v1) =>
			{
				if (level > ffmpeg.av_log_get_level()) return;
				var messageBufferLen = 1024; // is this too much for the stack?
				var messageBuffer = stackalloc byte[messageBufferLen];
				var printPrefix = 1;
				ffmpeg.av_log_format_line(p0, level, format, v1, messageBuffer, messageBufferLen, &printPrefix);
				var message = Marshal.PtrToStringAnsi((nint)messageBuffer);
				Console.Error.Write(message);
			};
			ffmpeg.av_log_set_callback(logCb);

			// format
			// ======
			Logger.Info("Creating AVFormatContext…");

			{
				AVFormatContext* fmtCtx = null;
				ffmpeg.avformat_alloc_output_context2(&fmtCtx, null, "matroska", Generator.OutputFilePath ?? "/dev/stdout");
				if (fmtCtx == null) Console.Error.WriteLine("cannot allocate AVFormatContext");
				_fmtCtx = fmtCtx;
			}
			if ((_fmtCtx->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
			{
				Logger.Debug("Format requested global stream headers.");
			}

			// encoders
			// ========
			Logger.Info("Creating encoders…");

			AVRational videoFps; videoFps.num = Generator.OutputFps; videoFps.den = 1;

			var videoEnc = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_H264);
			var audioEnc = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_AAC);

			_videoCtx = ffmpeg.avcodec_alloc_context3(videoEnc);
			_videoCtx->codec_type = AVMediaType.AVMEDIA_TYPE_VIDEO;
			_videoCtx->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;
			_videoCtx->width = Generator.OutputVideoWidth;
			_videoCtx->height = Generator.OutputVideoHeight;
			_videoCtx->time_base.num = 1;
			_videoCtx->time_base.den = videoFps.num;
			_videoCtx->framerate.num = videoFps.num;
			_videoCtx->framerate.den = videoFps.den;
			_videoCtx->bit_rate = OutputVideoBitRate;
			// _videoCtx->thread_count = Environment.ProcessorCount / 2;
			// Console.Error.WriteLine($"using {_videoCtx->thread_count} threads");
			if ((_fmtCtx->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
			{
				_videoCtx->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
			}
			// h264 codec fails with EINVAL/11 if extradata does not get allocated manually.
			if (_videoCtx->codec->id == AVCodecID.AV_CODEC_ID_H264)
			{
				_videoCtx->extradata = (byte*)ffmpeg.av_malloc(32);
				_videoCtx->extradata_size = 24;
			}
			AVDictionary* videoEncOpts;
			var ret = ffmpeg.avcodec_open2(_videoCtx, videoEnc, &videoEncOpts);
			FfmpegUtils.LogIfAvError(ret, "cannot open video codec");

			_audioCtx = ffmpeg.avcodec_alloc_context3(audioEnc);
			_audioCtx->codec_type = AVMediaType.AVMEDIA_TYPE_AUDIO;
			_audioCtx->sample_fmt = AVSampleFormat.AV_SAMPLE_FMT_FLTP;
			_audioCtx->sample_rate = Generator.AudioOutputSampleRate;
			_audioCtx->time_base.num = 1;
			_audioCtx->time_base.den = Generator.AudioOutputSampleRate;
			_audioCtx->ch_layout.nb_channels = 2;
			_audioCtx->ch_layout.order = AVChannelOrder.AV_CHANNEL_ORDER_NATIVE;
			_audioCtx->ch_layout.u.mask = ffmpeg.AV_CH_LAYOUT_STEREO;
			_audioCtx->bit_rate = 128_000;
			_audioCtx->extradata = (byte*)ffmpeg.av_mallocz(32);
			_audioCtx->extradata_size = 24;
			if ((_fmtCtx->oformat->flags & ffmpeg.AVFMT_GLOBALHEADER) != 0)
			{
				_audioCtx->flags |= ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;
			}
			ret = ffmpeg.avcodec_open2(_audioCtx, audioEnc, null);
			FfmpegUtils.LogIfAvError(ret, "cannot open audio codec");

			// streams
			// =======

			_videoStream = ffmpeg.avformat_new_stream(_fmtCtx, null);
			if (_videoStream == null) Logger.Error("cannot allocate video output stream");
			_videoStream->index = (int)(_fmtCtx->nb_streams - 1);
			_videoStream->time_base = _videoCtx->time_base;
			_videoStream->r_frame_rate = videoFps;

			ret = ffmpeg.avcodec_parameters_from_context(_videoStream->codecpar, _videoCtx);
			FfmpegUtils.LogIfAvError(ret, "cannot set video codec params from codec context");

			_audioStream = ffmpeg.avformat_new_stream(_fmtCtx, null);
			if (_videoStream == null) Logger.Error("cannot allocate audio output stream");
			_audioStream->index = (int)(_fmtCtx->nb_streams - 1);
			_audioStream->time_base.num = _audioCtx->time_base.num;
			_audioStream->time_base.den = _audioCtx->time_base.den;
			
			ret = ffmpeg.avcodec_parameters_from_context(_audioStream->codecpar, _audioCtx);
			FfmpegUtils.LogIfAvError(ret, "cannot set audio codec params from codec context");
			if (_audioStream->codecpar->extradata == null)
			{
				Logger.Error("audio codec did not create extradata buffer");
			}

			// output file/stream
			// ==================

			ret = ffmpeg.avio_open(&_fmtCtx->pb, Generator.OutputFilePath ?? "pipe:", Generator.OutputFilePath != null ? ffmpeg.AVIO_FLAG_READ_WRITE : ffmpeg.AVIO_FLAG_WRITE);
			FfmpegUtils.LogIfAvError(ret, "cannot open stdout");
			AVDictionary* fmtOpts;
			ret = ffmpeg.avformat_write_header(_fmtCtx, &fmtOpts);
			FfmpegUtils.LogIfAvError(ret, "cannot write header");

			byte* dictBuf = (byte*)ffmpeg.av_malloc(1024);
			ffmpeg.av_dict_get_string(fmtOpts, &dictBuf, (byte)'=', (byte)':');

			// video frames
			// ============

			_videoAvFrame = ffmpeg.av_frame_alloc();
			_videoAvFrame->format = (int)AVPixelFormat.AV_PIX_FMT_YUV420P;
			_videoAvFrame->width = Generator.OutputVideoWidth;
			_videoAvFrame->height = Generator.OutputVideoHeight;
			_videoAvFrame->time_base = _videoStream->time_base;

			ret = ffmpeg.av_frame_get_buffer(_videoAvFrame, 0);
			FfmpegUtils.LogIfAvError(ret, "cannot allocate video pixel buffer");

			_videoAvFramePre = ffmpeg.av_frame_alloc();
			_videoAvFramePre->format = (int)AVPixelFormat.AV_PIX_FMT_RGBA;
			_videoAvFramePre->width = Generator.OutputVideoWidth;
			_videoAvFramePre->height = Generator.OutputVideoHeight;
			_videoAvFramePre->time_base = _videoStream->time_base;

			ret = ffmpeg.av_frame_get_buffer(_videoAvFramePre, 0);
			FfmpegUtils.LogIfAvError(ret, "cannot allocate video pixel buffer");

			// audio frames
			// ============

			_audioAvFrame = ffmpeg.av_frame_alloc();
			_audioAvFrame->format = (int)AVSampleFormat.AV_SAMPLE_FMT_FLTP;
			ffmpeg.av_channel_layout_copy(&_audioAvFrame->ch_layout, &_audioCtx->ch_layout);
			_audioAvFrame->sample_rate = _audioCtx->sample_rate;
			_audioAvFrame->nb_samples = _audioCtx->frame_size;
			_audioAvFrame->ch_layout.nb_channels = Generator.AudioOutputChannelCount;
			_audioAvFrame->ch_layout.u.mask = 3;
			_audioAvFrame->time_base.num = _audioCtx->time_base.num;
			_audioAvFrame->time_base.den = _audioCtx->time_base.den;

			if ((_audioCtx->codec->capabilities & ffmpeg.AV_CODEC_CAP_VARIABLE_FRAME_SIZE) == 0)
			{
				Logger.Warning("audio codec does not support variable frame size");
			}

			ret = ffmpeg.av_frame_get_buffer(_audioAvFrame, 0);
			FfmpegUtils.LogIfAvError(ret, "cannot allocate audio sample buffer");
			_audioQueue.TriggerLength = _audioAvFrame->nb_samples * _audioAvFrame->ch_layout.nb_channels;
			_audioQueue.BufferLength = _audioQueue.TriggerLength * 4;
			_audioQueue.OutputCallback = (buf) =>
			{
				float* ab0 = (float*)_audioAvFrame->data[0];
				float* ab1 = (float*)_audioAvFrame->data[1];
				for (int i = 0; i < _audioAvFrame->linesize[0] / sizeof(float); i++)
				{
					ab0[i] = buf[i * 2];
					ab1[i] = buf[i * 2 + 1];
				}

				DoEncode(_audioCtx, _audioStream, _audioAvFrame, _audioAvPacket);

				// if (_audioAvFrame->pts != _lastAudioPts + 1024)
				// {
				// 	Logger.Warning($"Audio PTS didn't advance normally: {_lastAudioPts} → {_audioAvFrame->pts}");
				// }
				// _lastAudioPts = _audioAvFrame->pts;
				// _currentAudioPts += _audioAvFrame->nb_samples;
			};

			Logger.Debug($"video original linesize = {_videoAvFramePre->linesize[0]} {_videoAvFramePre->linesize[1]}");
			Logger.Debug($"video target linesize =   {_videoAvFrame->linesize[0]} {_videoAvFrame->linesize[1]} {_videoAvFrame->linesize[2]}");
			Logger.Debug($"req. audio frame size =   {_audioCtx->frame_size} * {_audioCtx->ch_layout.nb_channels}ch");
			Logger.Debug($"audio ch layout =         {_audioAvFrame->ch_layout.nb_channels} {_audioAvFrame->ch_layout.order} {_audioAvFrame->ch_layout.u.mask}");
			Logger.Debug($"audio linesizes =         {_audioAvFrame->linesize[0]} {_audioAvFrame->linesize[1]} {_audioAvFrame->linesize[2]} {_audioAvFrame->linesize[3]} {_audioAvFrame->linesize[4]} {_audioAvFrame->linesize[5]} {_audioAvFrame->linesize[6]} {_audioAvFrame->linesize[7]}");

			_videoAvPacket = ffmpeg.av_packet_alloc();
			_audioAvPacket = ffmpeg.av_packet_alloc();

			ffmpeg.av_dump_format(_fmtCtx, 0, Generator.OutputFilePath ?? "pipe:", 1);

			// swsctx
			// ======

			if (_swsCtx == null)
			{
				_swsCtx = ffmpeg.sws_getContext(Generator.OutputVideoWidth, Generator.OutputVideoHeight, (AVPixelFormat)_videoAvFramePre->format, _videoAvFrame->width, _videoAvFrame->height, (AVPixelFormat)_videoAvFrame->format, (int)SwsFlags.SWS_BILINEAR, null, null, null);
				if (_swsCtx == null)
				{
					Logger.Error("cannot initialize sws context");
				}
			}
			// TODO: move to init method
			if (_swsCtx == null)
			{
				_swsCtx = ffmpeg.sws_getContext(Generator.OutputVideoWidth, Generator.OutputVideoHeight, (AVPixelFormat)_videoAvFramePre->format, _videoAvFrame->width, _videoAvFrame->height, (AVPixelFormat)_videoAvFrame->format, (int)SwsFlags.SWS_BILINEAR, null, null, null);
				if (_swsCtx == null)
				{
					Logger.Error("cannot initialize sws context");
				}
			}
		}
		_init = true;
	}

	private unsafe void DoEncode(AVCodecContext* cCtx, AVStream* stream, AVFrame* frame, AVPacket* packet)
	{
		int ret;

		FfmpegUtils.LogFrameData(frame);

		ret = ffmpeg.avcodec_send_frame(cCtx, frame);
		FfmpegUtils.LogIfAvError(ret, "cannot send frame to encoder");

		if (frame != null) Logger.Debug($"Sent AVFrame to encoder: stream #{stream->index}, PTS {frame->pts}, duration {frame->duration}, timebase {frame->time_base.num}/{frame->time_base.den}.");

		while (ret >= 0)
		{
			ret = ffmpeg.avcodec_receive_packet(cCtx, packet);

			if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN))
			{
				break;
			}
			else if (ret < 0)
			{
				// if frame is null, `EOF` code is expected, don't treat it as an error.
				if (frame != null)
				{
					FfmpegUtils.LogIfAvError(ret, "cannot encode");
				}
				break;
			}

			ffmpeg.av_packet_rescale_ts(packet, cCtx->time_base, stream->time_base);
			packet->stream_index = stream->index;
			packet->time_base.num = stream->time_base.num;
			packet->time_base.den = stream->time_base.den;
			FfmpegUtils.LogPacketData(packet);

			ret = ffmpeg.av_interleaved_write_frame(_fmtCtx, packet);
			FfmpegUtils.LogIfAvError(ret, "cannot write packet");
			if (ret == -32) Generator._exitRequested = true;
		}
	}

	public void PushNewFrame(Image videoFrame, AudioBuffer audioFrame, double delta)
	{
		PushNewFrame((Image<Rgba32>)videoFrame, audioFrame, delta);
	}

	public unsafe void PushNewFrame(Image<Rgba32> videoFrame, AudioBuffer audioFrame, double delta)
	{
		if (!_init)
		{
			InitializeFfmpeg();
		}

		var ret = ffmpeg.av_frame_make_writable(_videoAvFrame);
		FfmpegUtils.LogIfAvError(ret, "cannot make video pixel data writable");
		ret = ffmpeg.av_frame_make_writable(_audioAvFrame);
		FfmpegUtils.LogIfAvError(ret, "cannot make audio sample buffer writable");

		if (_swsCtx != null)
		{
			var pixelData = new byte[videoFrame.Width * videoFrame.Height * 4];
			videoFrame.CopyPixelDataTo(pixelData);

			Marshal.Copy(pixelData, 0, (nint)_videoAvFramePre->data[0], pixelData.Length);
			ffmpeg.sws_scale(_swsCtx, _videoAvFramePre->data, _videoAvFramePre->linesize, 0, _videoAvFramePre->height, _videoAvFrame->data, _videoAvFrame->linesize);
		}
		else if (_videoAvFrame->format == (int)AVPixelFormat.AV_PIX_FMT_GBRP)
		{
			// Unoptimized pixel copy.
			videoFrame.ProcessPixelRows((pa) =>
			{
				for (int y = 0; y < pa.Height; y++)
				{
					var row = pa.GetRowSpan(y);

					for (int x = 0; x < pa.Width; x++)
					{
						var p = row[x];
						_videoAvFrame->data[0][_videoAvFrame->linesize[0] * y + x] = p.G;
						_videoAvFrame->data[1][_videoAvFrame->linesize[1] * y + x] = p.B;
						_videoAvFrame->data[2][_videoAvFrame->linesize[2] * y + x] = p.R;
					}
				}
			});
		}

		_videoAvFrame->time_base.num = _videoCtx->time_base.num;
		_videoAvFrame->time_base.den = _videoCtx->time_base.den;
		_videoAvFrame->pts = _frameNum;
		_videoAvFrame->duration = 1;
		DoEncode(_videoCtx, _videoStream, _videoAvFrame, _videoAvPacket);

		//_audioAvFrame->pts = (long)Math.Round(_audioAvFrame->sample_rate * (_frameNum / (double)Generator.OutputFps));
		//_audioAvFrame->pts = (long)(_audioAvFrame->nb_samples * (_audioAvFrame->pts / (double)Generator.AudioOutputSamplesPerFramePerChannel));
		//_audioAvFrame->pts = _currentAudioPts;
		_audioAvFrame->pts = (long)(_frameNum * Generator.AudioOutputSamplesPerFramePerChannel / (double)_audioAvFrame->nb_samples) * _audioAvFrame->nb_samples;
		_audioAvFrame->duration = _audioAvFrame->nb_samples;
		_audioQueue.Push(audioFrame.ToArray());

		Logger.Trace($"Encoded: frame {_frameNum}, audio PTS {_audioAvFrame->pts}, video PTS {_videoAvFrame->pts}, framegen speed {(int)(1/delta)} FPS");

		_frameNum++;
	}

	public unsafe void Finish()
	{
		Logger.Debug("Flushing streams…");
		DoEncode(_videoCtx, _videoStream, null, _videoAvPacket);
		DoEncode(_audioCtx, _audioStream, null, _audioAvPacket);

		Logger.Debug("Freeing FFmpeg resources…");
		ffmpeg.sws_freeContext(_swsCtx);
		var videoCtx = _videoCtx;
		ffmpeg.avcodec_free_context(&videoCtx);
		var audioCtx = _audioCtx;
		ffmpeg.avcodec_free_context(&audioCtx);

		ffmpeg.av_write_trailer(_fmtCtx);

		if ((_fmtCtx->flags & ffmpeg.AVFMT_NOFILE) == 0)
		{
			ffmpeg.avio_closep(&_fmtCtx->pb);
		}

		ffmpeg.avformat_free_context(_fmtCtx);
	}
}
