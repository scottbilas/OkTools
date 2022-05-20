using System.Threading.Tasks.Dataflow;
using Vezel.Cathode;
using Vezel.Cathode.Text.Control;

class TerminalNotInteractiveException : Exception {}
class TerminalInputEofException : Exception {}

interface IEvent {}

// TODO: split into KeyEvent vs CharEvent...the union is not very helpful, especially given pattern matching
// (and don't want to use ConsoleKey.A/B/C etc. because there is no '!' or '#' etc. in ConsoleKey, even the tiny oem set)
[PublicAPI]
readonly struct KeyEvent : IEvent
{
    public readonly ConsoleKeyInfo Key;

    public KeyEvent(ConsoleKeyInfo key) => Key = key;
    public KeyEvent(ConsoleKey key, bool shift, bool alt, bool ctrl) : this(new ConsoleKeyInfo((char)0, key, shift, alt, ctrl)) {}
    // ReSharper disable once IntroduceOptionalParameters.Global
    public KeyEvent(ConsoleKey key) : this(key, false, false, false) {}
    public KeyEvent(char ch, bool alt, bool ctrl) : this(new ConsoleKeyInfo(ch, 0, char.IsUpper(ch), alt, ctrl)) {}
}

[PublicAPI]
readonly struct ErrorEvent : IEvent
{
    public readonly Exception Exception;

    public ErrorEvent(Exception exception) => Exception = exception;
}

class Screen : IDisposable
{
    static int s_instanceCount;
    bool _engaged, _disposed;

    readonly BufferBlock<IEvent> _events = new();
    readonly BufferBlock<ReadOnlyMemory<byte>> _rawInput = new();

    public Screen()
    {
        if (s_instanceCount > 0)
            throw new InvalidOperationException("Instance already exists");
        ++s_instanceCount;

        Terminal = Vezel.Cathode.Terminal.System;
        if (!Terminal.StandardIn.IsInteractive)
            throw new TerminalNotInteractiveException();

        /* https://github.com/gdamore/tcell/v2/tscreen.go

	        t.enableMouse(t.mouseFlags)
	        t.enablePasting(t.pasteEnabled)

	        ti := t.ti
	        t.TPuts(ti.EnterCA)
	        t.TPuts(ti.EnterKeypad)
	        t.TPuts(ti.HideCursor)
	        t.TPuts(ti.EnableAcs)
	        t.TPuts(ti.Clear)
        */

        Terminal.EnableRawMode();
        Terminal.Out(new ControlBuilder()
            .SetScreenBuffer(ScreenBuffer.Alternate)
            .SetCursorVisibility(false)
            .ClearScreen());
        Terminal.Signaled += OnSignaled;

        _engaged = true;

        void Wrap(Action action)
        {
            try
            {
                action();
            }
            catch (Exception x)
            {
                _events.Post(new ErrorEvent(x));
            }
        }

        Task.Run(() => Wrap(TaskReadRawInput));
        Task.Run(() => Wrap(TaskReadKeyEvents));
    }

    public VirtualTerminal Terminal { get; }

    public IEvent? TryGetEvent()
    {
        _events.TryReceive(out var evt);
        return evt;
    }

    void OnSignaled(TerminalSignalContext _) => Disengage();

    public void Dispose()
    {
        if (_disposed)
            throw new ObjectDisposedException("Instance already disposed");

        Disengage();

        _disposed = true;
        --s_instanceCount;
    }

    void Disengage()
    {
        if (!_engaged)
            return;

        _engaged = false;

        /* https://github.com/gdamore/tcell/v2/tscreen.go

            // shutdown the screen and disable special modes (e.g. mouse and bracketed paste)
            ti := t.ti
            t.cells.Resize(0, 0)
            t.TPuts(ti.ShowCursor)
            if t.cursorStyles != nil && t.cursorStyle != CursorStyleDefault {
                t.TPuts(t.cursorStyles[t.cursorStyle])
            }
            t.TPuts(ti.ResetFgBg)
            t.TPuts(ti.AttrOff)
            t.TPuts(ti.Clear)
            t.TPuts(ti.ExitCA)
            t.TPuts(ti.ExitKeypad)
            t.enableMouse(0)
            t.enablePasting(false)
        */

        Terminal.Signaled -= OnSignaled;
        Terminal.Out(new ControlBuilder()
            .ResetAttributes()
            .ClearScreen()
            .SetScreenBuffer(ScreenBuffer.Main)
            .SetCursorVisibility(true));
        Terminal.DisableRawMode();
    }

    void TaskReadRawInput()
    {
        // vezel cathode doesn't support cancelable key read on windows, so we have to roll our own
        //
        // see:
        //
        //  * https://github.com/vezel-dev/cathode/issues/63 (Support input cancellation on Windows)
        //  * https://github.com/vezel-dev/cathode/issues/59 (Implement a control sequence parser)
        //

        // TODO: would really like to use a cancelable Terminal.Read or use a Peek() on there instead, so we can
        // get rid of this unnecessary extra _rawInput protocol.
        // (keep the combined read+process in a task off the main thread, though.)

        while (_engaged)
        {
            var input = new byte[128];
            var read = Terminal.Read(input);
            if (read == 0)
                throw new TerminalInputEofException();

            _rawInput.Post(new Memory<byte>(input, 0, read));
        }
    }

    void TaskReadKeyEvents()
    {
        var parser = new InputParser(_rawInput, _events);
        while (_engaged)
            parser.Process();
    }
}
