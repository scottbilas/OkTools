using Spectre.Console;

class StatusPane
{
    readonly Screen _screen;
    readonly string _color;
    string _text = "";

    // FUTURE: get rid of 'color' and do a segment config approach like starship etc.
    public StatusPane(Screen screen, int top, string color)
    {
        _screen = screen;
        _color = color;
        Top = top;
    }

    public int Top;

    public void Invalidate() => _text = "";

    public void Print(string text)
    {
        _text = text;

        _screen.Control.PrintMarkup(text.Length <= _screen.ScreenWidth
            ? $"[{_color}]{text.PadRight(_screen.ScreenWidth).EscapeMarkup()}[/]"
            : $"[{_color}]{text[..(_screen.ScreenWidth-Constants.WrapText.Length)].EscapeMarkup()}[/]"+
              $"[{Constants.WrapColor}]{Constants.WrapText}[/]");
    }

    public void Update(string text)
    {
        if (_text == text)
            return;

        using var _ = _screen.SaveRestoreCursorPos(0, Top, flushOnDispose: true);
        Print(text);
    }
}
