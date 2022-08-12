abstract class ViewBase
{
    bool _enabled;

    protected ViewBase(Screen screen)
    {
        Screen = screen;
    }

    public Screen Screen { get; }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
                return;

            _enabled = value;
            OnEnabledChanged(_enabled);
        }
    }

    public int Width { get; private set; }
    public int Top { get; private set; }
    public int Bottom { get; private set; }
    public int Height => Bottom - Top;

    public virtual void SetBounds(int width, int top, int bottom)
    {
        CheckEnabled();

        Width = width;
        Top = top;
        Bottom = bottom;
    }

    protected virtual void OnEnabledChanged(bool enabled) {}

    protected void CheckEnabled()
    {
        if (!Enabled)
            throw new InvalidOperationException("Unexpected operation on a disabled View");
    }
}
