using System.Drawing;
using OkTools.Core.Terminal;
using Spectre.Console;
using Vezel.Cathode.Text.Control;

static class ControlBuilderExtensions
{
    public static void PrintMarkupLine(this ControlBuilder @this, string text) =>
        @this.PrintLine(new Markup(text).ToAnsi());
    public static void PrintMarkup(this ControlBuilder @this, string text) =>
        @this.Print(new Markup(text).ToAnsi());
    public static void PrintMarkupLineInterp(this ControlBuilder @this, FormattableString value) =>
        @this.PrintLine(Markup.FromInterpolated(value).ToAnsi());
    public static void PrintMarkupInterp(this ControlBuilder @this, FormattableString value) =>
        @this.Print(Markup.FromInterpolated(value).ToAnsi());
}

partial class Screen
{
    readonly ControlBuilder _cb = new();

    public ControlBuilder Control => _cb;

    public void FlushStdout()
    {
        if (_cb.Span.IsEmpty)
            return;

        _terminal.Out(_cb.Span);
        _cb.Clear(10*1024);
    }

    public void SetCursorPos(int x, int y)
    {
        if (x < 0 || x >= _screenSize.Width)
            throw new ArgumentOutOfRangeException(nameof(x), $"Out of range 0 <= {x} < {_screenSize.Width}");
        if (y < 0 || y >= _screenSize.Height)
            throw new ArgumentOutOfRangeException(nameof(y), $"Out of range 0 <= {y} < {_screenSize.Height}");

        _cb.MoveCursorTo(y, x);
    }

#   pragma warning disable CA1822, RS0030
    public Point GetCursorPos()
    {
        // may be lingering move ops
        FlushStdout();

        // cathode does not yet support reading cursor position. see https://github.com/vezel-dev/cathode/discussions/16
        // i get the concern, but for this app i'm ensuring that i do cursor reading at well-defined times.
        var (left, top) = Console.GetCursorPosition();
        return new Point(left, top);
    }
#   pragma warning restore CA1822, RS0030

    public AutoSaveRestoreCursorState SaveRestoreCursorPos(bool showOnDispose = false, bool flushOnDispose = false) =>
        new(this, showOnDispose, flushOnDispose);
    public AutoSaveRestoreCursorState SaveRestoreCursorPos(int x, int y, bool showOnDispose = false, bool flushOnDispose = false)
    {
        var saver = new AutoSaveRestoreCursorState(this, showOnDispose, flushOnDispose);
        SetCursorPos(x, y);
        return saver;
    }

    public readonly struct AutoSaveRestoreCursorState : IDisposable
    {
        readonly Screen _screen;
        readonly bool _showOnDispose, _flushOnDispose;

        public AutoSaveRestoreCursorState(Screen screen, bool showOnDispose = false, bool flushOnDispose = false)
        {
            _screen = screen;
            _showOnDispose = showOnDispose;
            _flushOnDispose = flushOnDispose;

            _screen.Control.SaveCursorState();
            _screen.Control.HideCursor();
        }

        public void Dispose()
        {
            _screen.Control.HideCursor();
            _screen.Control.RestoreCursorState();

            if (_showOnDispose)
                _screen.Control.ShowCursor();
            if (_flushOnDispose)
                _screen.FlushStdout();
        }
    }
}
