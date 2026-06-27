namespace Unai.ExtendedBinaryWaterfall.Gui.RlImGui;

public abstract class UiPanel
{
	protected bool _open = false;
	protected bool _focused = false;

	public bool Open { get => _open; set => _open = value; }
	public bool Focused => _focused;

	public abstract void Setup();
	public abstract void Shutdown();
	public abstract void Show();
	public abstract void Update();
}
