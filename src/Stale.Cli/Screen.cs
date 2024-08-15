using System.Drawing;
using OkTools.Core.Terminal;
using Vezel.Cathode;
using Vezel.Cathode.Text.Control;

public enum ScreenUpdateStatus { Ok, TerminalSizeChanged }

class Screen : IDisposable
{
    readonly ControlBuilder _cb = new();
    bool _disposed;

    readonly VirtualTerminal _terminal;

    Size _screenSize;
    Size? _pendingScreenSize;

    public Screen(VirtualTerminal terminal)
    {
        _terminal = terminal;
        _terminal.Resized += OnResized;
        _pendingScreenSize = _terminal.Size;

        Control.SoftReset();
        Control.HideCursor();

        Update();
    }

    public ControlBuilder Control => _cb;

    public void Dispose()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _disposed = true;

        // set terminal back to normal, we may have leftover state like margins or whatever
        _cb.Clear();
        _cb.SoftReset();
        FlushStdout();

        _terminal.Resized -= OnResized;
    }

    public Size ScreenSize => _screenSize;
    public int ScreenWidth => _screenSize.Width;
    public int ScreenHeight => _screenSize.Height;

    public ScreenUpdateStatus Update()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_pendingScreenSize is null)
            return ScreenUpdateStatus.Ok;

        _screenSize = _pendingScreenSize.Value;
        _pendingScreenSize = null;

        return ScreenUpdateStatus.TerminalSizeChanged;
    }

    void OnResized(Size newSize) => _pendingScreenSize = newSize;

    public async ValueTask FlushStdoutAsync(CancellationToken cancel)
    {
        if (_cb.Span.IsEmpty)
            return;

        await _terminal.OutAsync(_cb.Memory, cancel);
        _cb.Clear(10*1024);
    }

    public void FlushStdout()
    {
        if (_cb.Span.IsEmpty)
            return;

        _terminal.Out(_cb.Memory);
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

    public AutoSaveRestoreCursorState SaveRestoreCursorPos() =>
        new(this);
    public AutoSaveRestoreCursorState SaveRestoreCursorPos(int x, int y)
    {
        var saver = new AutoSaveRestoreCursorState(this);
        SetCursorPos(x, y);
        return saver;
    }

    public readonly struct AutoSaveRestoreCursorState : IDisposable
    {
        readonly Screen _screen;

        public AutoSaveRestoreCursorState(Screen screen)
        {
            _screen = screen;
            _screen.Control.SaveCursorState();
            _screen.Control.HideCursor();
        }

        public void Dispose()
        {
            _screen.Control.HideCursor();
            _screen.Control.RestoreCursorState();
        }
    }
}
