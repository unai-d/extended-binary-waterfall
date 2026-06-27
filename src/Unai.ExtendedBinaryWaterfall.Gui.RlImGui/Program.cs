using Raylib_cs;
using rlImGui_cs;
using ImGuiNET;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Unai.ExtendedBinaryWaterfall.Parsers.Custom;
using System.Reflection;
using Unai.ExtendedBinaryWaterfall.Parsers;
using Unai.ExtendedBinaryWaterfall.Gui.RlImGui.Exporters;
using System.Globalization;

namespace Unai.ExtendedBinaryWaterfall.Gui.RlImGui;

class Program
{
	static bool _exitRequested = false;
	static ImFontPtr _defaultFont = null;
	internal static UiViewportPanel _viewportPanel = new();
	static List<UiPanel> _panels = [ _viewportPanel ];
	
	internal static Generator _generator = new();
	static readonly string _nullParserId = typeof(CustomParser).GetCustomAttribute<ParserAttribute>().Id;

	static void InitializeFonts()
	{
		string[] searchPaths = Environment.OSVersion.Platform switch
		{
			PlatformID.Win32NT => ["C:\\WINDOWS\\Fonts"],
			PlatformID.Unix => ["/usr/share/fonts"],
			_ => throw new NotImplementedException("Cannot determine path for font searching in unknown platform."),
		};

		if (searchPaths == null) return;

		rlImGui.SetupUserFonts += (ImGuiIOPtr imGuiIo) =>
		{
			var fontFiles = Directory.GetFiles(searchPaths[0], "*.*", SearchOption.AllDirectories); // TODO: Iterate array!!
			var fontPaths = fontFiles.Where(fp => fp.EndsWith("DejaVuSans.ttf"));
			foreach (var fontPath in fontPaths)
			{
				Logger.Debug($"Selected font for ImGUI: '{fontPath}'");
				_defaultFont = imGuiIo.Fonts.AddFontFromFileTTF(fontPath, 16);
				break;
			}
		};
	}

	static void DoMainMenuBar()
	{
		if (ImGui.BeginMainMenuBar())
		{
			if (ImGui.BeginMenu("File"))
			{
				if (ImGui.MenuItem("Open..."))
				{
					_generator.InputFilePath = "/usr/bin/ffmpeg";
					_generator.Initialize();
					_generator.GenerateFrame(0);
				}

				if (ImGui.MenuItem("Exit"))
				{
					_exitRequested = true;
				}

				ImGui.EndMenu();
			}

			if (ImGui.BeginMenu("View"))
			{
				if (ImGui.MenuItem("Viewport Panel"))
				{
					_viewportPanel.Open = true;
				}

				ImGui.EndMenu();
			}

			ImGui.EndMainMenuBar();
		}
	}

	static void Main(string[] args)
	{
		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
		
		Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint | ConfigFlags.ResizableWindow);
		Raylib.InitWindow(1280, 720, $"{BuildInfo.ApplicationName} {BuildInfo.SemVer}");
		Raylib.SetTargetFPS(120);

		InitializeFonts();

		rlImGui.Setup(true);

		ImGui.GetIO().ConfigWindowsMoveFromTitleBarOnly = true;

		foreach (var panel in _panels)
		{
			Logger.Debug($"Initializing panel: {panel.GetType().Name}");
			panel.Setup();
			panel.Open = true;
		}

		_generator.Exporter = new InternalExporter(_generator);

		while (!Raylib.WindowShouldClose() && !_exitRequested)
		{
			foreach (var panel in _panels)
			{
				panel.Update();
			}

			Raylib.BeginDrawing();
			Raylib.ClearBackground(Color.Black);

			rlImGui.Begin();
			ImGui.PushFont(_defaultFont);

			DoMainMenuBar();

			foreach (var panel in _panels)
			{
				if (panel.Open) panel.Show();
			}
			
			ImGui.PopFont();
			rlImGui.End();

			Raylib.EndDrawing();
		}

		rlImGui.Shutdown();

		foreach (var panel in _panels)
		{
			panel.Shutdown();
		}

		Raylib.CloseWindow();
	}
}
