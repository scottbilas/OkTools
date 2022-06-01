abstract class ViewBase
{
    public Screen Screen { get; }
    public bool Enabled { get; set; }
    public int Width { get; private set; }
    public int Top { get; private set; }
    public int Bottom { get; private set; }
    public int Height => Bottom - Top;

    protected ViewBase(Screen screen)
    {
        Screen = screen;
    }

    public virtual void SetBounds(int width, int top, int bottom, bool forceRedraw)
    {
        if (!Enabled)
            throw new InvalidOperationException("Unexpected operation on a disabled View");

        Width = width;
        Top = top;
        Bottom = bottom;
    }
}
