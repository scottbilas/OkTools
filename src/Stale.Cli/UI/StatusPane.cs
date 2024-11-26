using OkTools.Terminal;
using Spectre.Console;
using Vezel.Cathode.Text.Control;

class StatusPane
{
    readonly string _color;
    string _text = "";

    // FUTURE: get rid of 'color' and do a segment config approach like starship etc.
    public StatusPane(int top, int maxWidth, string color)
    {
        _color = color;
        MaxWidth = maxWidth;
        Top = top;
    }

    public int Top { get; set; }
    public int MaxWidth { get; set; }

    public void Invalidate() => _text = "";

    public void Print(ControlBuilder cb, string text)
    {
        _text = text;

        cb.PrintMarkup(text.Length <= MaxWidth
            ? $"[{_color}]{text.PadRight(MaxWidth).EscapeMarkup()}[/]"
            : $"[{_color}]{text[..(MaxWidth-Constants.WrapText.Length)].EscapeMarkup()}[/]"+
              $"[{Constants.WrapColor}]{Constants.WrapText}[/]");
    }

    public void PrintLine(ControlBuilder cb, string text)
    {
        Print(cb, text);
        cb.PrintLine();
    }

    public void Update(ControlBuilder cb, string text)
    {
        // TODO: maybe find a span to ffwd to and print limited update (for when simple stuff like coords is changing)
        // (but don't overengineer..just very basic test to reduce update size)

        if (_text == text)
            return;

        using var _ = cb.SaveRestoreCursor(Top, 0);
        Print(cb, text);
    }
}
