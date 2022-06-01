using OkTools.Core;

class StatusView : ViewBase
{
    Memory<char> _text;

    public StatusView(Screen screen) : base(screen) {}

    public override void SetBounds(int width, int top, int bottom, bool forceRedraw)
    {
        base.SetBounds(width, top, bottom, forceRedraw);

        if (_text.Length != Width)
            _text = new Memory<char>(new char[width]);

        Refresh();
    }

    void Refresh()
    {
        Screen.OutSetCursorPos(0, Top);
        //Screen.OutPrint(_text);
    }
}
