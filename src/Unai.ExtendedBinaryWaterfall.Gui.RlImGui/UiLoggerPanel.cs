using ImGuiNET;

namespace Unai.ExtendedBinaryWaterfall.Gui.RlImGui;

public class UiLoggerPanel : UiPanel
{
	public override void Setup()
	{
		
	}

	public override void Show()
	{
		if (ImGui.Begin("Console Logger", ref _open))
		{
			if (ImGui.BeginTable("logs", 3))
			{
				foreach (var le in InternalLogger.History)
				{
					ImGui.TableNextColumn();
					ImGui.TextUnformatted($"{le.TimestampOffset}");
					ImGui.TableNextColumn();
					ImGui.TextUnformatted($"{le.StackMethodName}");
					ImGui.TableNextColumn();
					ImGui.TextUnformatted(le.Message);
				}

				ImGui.EndTable();
			}

			ImGui.End();
		}
	}

	public override void Shutdown()
	{
		
	}

	public override void Update()
	{
		if (!_open) return;
	}
}