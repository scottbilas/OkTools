using System.Drawing;
using OkTools.Terminal;
using Vezel.Cathode.Text.Control;

interface IEvent;
readonly record struct InputEvent(KeyEvent KeyEvent) : IEvent;
readonly record struct TerminalSizeEvent(Size NewSize) : IEvent;

readonly struct SaveRestoreCursor : IDisposable
{
    readonly Action<Action<ControlBuilder>> _outControl;

    public SaveRestoreCursor(Action<Action<ControlBuilder>> outControl)
    {
        _outControl = outControl;
        _outControl(c => c
            .SaveCursorState()
            .SetCursorVisibility(false));
    }

    public void Dispose()
    {
        _outControl(c => c
            .ResetAttributes()
            .RestoreCursorState()
            .SetCursorVisibility(true));
    }
}

readonly struct AlternateScreen : IDisposable
{
    readonly Action<Action<ControlBuilder>> _outControl;

    public AlternateScreen(Action<Action<ControlBuilder>> outControl)
    {
        _outControl = outControl;
        _outControl(c => c.SetScreenBuffer(ScreenBuffer.Alternate));
    }

    public void Dispose()
    {
        _outControl(c => c.SetScreenBuffer(ScreenBuffer.Main));
    }
}
