class StatusView : ViewBase
{
    string _text;

    public StatusView(Screen screen) : base(screen)
    {
    }

    public override void SetBounds(int width, int top, int bottom)
    {
        base.SetBounds(width, top, bottom);

        Refresh();
    }

    public void SetStatus(string logName, int filterCount, int currentFilter)
    {

    }

    void Refresh()
    {
        Screen.OutSetCursorPos(0, Top);
        Screen.OutPrint(_text);
    }
}
