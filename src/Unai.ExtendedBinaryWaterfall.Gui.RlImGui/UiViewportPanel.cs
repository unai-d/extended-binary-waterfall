using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;

namespace Unai.ExtendedBinaryWaterfall.Gui.RlImGui;

public class UiViewportPanel : UiPanel
{
	int _currentFrame = 0;

	Task _playbackTask = Task.CompletedTask;
	CancellationTokenSource _cts;
	CancellationToken _playbackCt;
	bool _isPlaying = false;

	RenderTexture2D _viewTex;
	internal Image _image;
	internal Texture2D _imageTex;
	internal bool _imageHasChanged = false;
	Camera2D _cam = new();

	public override void Setup()
	{
		_cam.Zoom = .5f;
		_cam.Target.X = 0;
		_cam.Target.Y = 0;
		_cam.Rotation = 0;
		_cam.Offset.X = Raylib.GetScreenWidth() / 2f;
		_cam.Offset.Y = Raylib.GetScreenHeight() / 2f;

		Raylib.SetTextureWrap(_imageTex, TextureWrap.Clamp);
		Raylib.SetTextureFilter(_imageTex, TextureFilter.Bilinear);
		Raylib.SetTextureWrap(_viewTex.Texture, TextureWrap.Clamp);
		Raylib.SetTextureFilter(_viewTex.Texture, TextureFilter.Bilinear);

		_viewTex = Raylib.LoadRenderTexture(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());

		UpdateRenderTexture();
	}

	public override void Show()
	{
		ImGui.SetNextWindowSizeConstraints(new Vector2(480, 270), new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight()));
		ImGui.SetNextWindowSize(new Vector2(Raylib.GetScreenWidth(), Raylib.GetScreenHeight()));
		ImGui.SetNextWindowPos(new Vector2(0, 20));

		if (ImGui.Begin("Viewport", ref _open, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar))
		{
			_focused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);

			Vector2 sizeAvail = ImGui.GetContentRegionAvail();
			sizeAvail.Y -= 64;

			Raylib_cs.Rectangle viewRect = new()
			{
				X = _viewTex.Texture.Width / 2 - sizeAvail.X / 2,
				Y = _viewTex.Texture.Height / 2 - sizeAvail.Y / 2,
				Width = sizeAvail.X,
				Height = -sizeAvail.Y
			};

			rlImGui.ImageRect(_viewTex.Texture, (int)sizeAvail.X, (int)sizeAvail.Y, viewRect);

			if (ImGui.BeginChild("Toolbar", new Vector2(ImGui.GetContentRegionAvail().X, 64)))
			{
				if (ImGui.Button("Play/Pause"))
				{
					TogglePlayback();
				}
				ImGui.SameLine();

				if (ImGui.SliderInt("Frame", ref _currentFrame, 0, (int)Program._generator.TotalFrames))
				{
					Program._generator.GenerateFrame(_currentFrame);
				}
				ImGui.SameLine();

				if (Program._generator.TotalFrames > 0)
				{
					ImGui.TextUnformatted(TimeSpan.FromSeconds(_currentFrame / Program._generator.TotalFrames).ToString());
				}
				ImGui.SameLine();

				if (ImGui.SliderFloat("Zoom", ref _cam.Zoom, 0.125f, 8f))
				{
					UpdateRenderTexture();
				}

			 	ImGui.EndChild();
			}

			ImGui.End();
		}
	}

	public override void Shutdown()
	{
		Raylib.UnloadRenderTexture(_viewTex);
		Raylib.UnloadTexture(_imageTex);
	}

	public override void Update()
	{
		if (!_open) return;

		if (_imageHasChanged)
		{
			_imageHasChanged = false;

			Raylib.UnloadTexture(_imageTex);
			_imageTex = Raylib.LoadTextureFromImage(_image);

			UpdateRenderTexture();
		}

		if (_playbackTask.IsFaulted)
		{
			Logger.Error($"Playback task failed: {_playbackTask.Exception}");
		}
	}

	void TogglePlayback()
	{
		Logger.Debug($"Toggling playback state… (is playing?: {_isPlaying})");

		_cts = new();
		_playbackCt = _cts.Token;
		
		if (_isPlaying)
		{
			_cts.Cancel();
		}
		else
		{
			_playbackTask = Task.Run(DoPlaybackLoop, _playbackCt);
		}

		_isPlaying = !_isPlaying;
	}

	async void DoPlaybackLoop()
	{
		Stopwatch sw = new();

		sw.Start();

		while (_currentFrame < Program._generator.TotalFrames)
		{
			Program._generator.GenerateFrame(_currentFrame);

			var elapsed = sw.Elapsed.TotalSeconds;
			var elapsedFrames = Program._generator.OutputFps * elapsed;

			_currentFrame += Math.Max((int)elapsedFrames, 1);

			if (elapsedFrames < 1)
			{
				await Task.Delay((int)(1000 * (1 - elapsedFrames) / Program._generator.OutputFps), CancellationToken.None);
			}

			sw.Restart();

			if (_playbackCt.IsCancellationRequested)
			{
				_isPlaying = false;
				return;
			}
		}

		_currentFrame = (int)Program._generator.TotalFrames - 1;

		Logger.Debug("Playback task ended.");

		_isPlaying = false;
	}

	internal void UpdateRenderTexture()
	{
		Raylib.BeginTextureMode(_viewTex);
		Raylib.ClearBackground(Color.DarkGray);
		Raylib.BeginMode2D(_cam);
		if (_imageTex.Id != 0) Raylib.DrawTexture(_imageTex, _imageTex.Width / -2, _imageTex.Height / -2, Color.White);
		Raylib.EndMode2D();
		Raylib.EndTextureMode();
	}
}
