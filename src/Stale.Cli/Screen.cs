using OkTools.Core.Terminal;
using Vezel.Cathode;
using Size = System.Drawing.Size;

public enum ScreenUpdateStatus { Ok, TerminalSizeChanged }

partial class Screen : IDisposable
{
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

    public void Dispose()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _disposed = true;

        // set terminal back to normal, we may have leftover state like margins or whatever
        _cb.Clear();
        _cb.SoftReset();
        _terminal.Out(_cb);

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
}
