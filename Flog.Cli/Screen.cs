using System.Threading.Tasks.Dataflow;

[PublicAPI]
readonly struct ErrorEvent : IEvent
{
    public readonly Exception Exception;

    public ErrorEvent(Exception exception) => Exception = exception;
}

partial class Screen : IDisposable
{
    static int s_instanceCount;
    volatile bool _disposed;

    readonly VirtualTerminal _terminal;
    readonly BufferBlock<IEvent> _events = new();
    readonly BufferBlock<ReadOnlyMemory<byte>> _rawInput = new();

    public Screen()
    {
        if (s_instanceCount > 0)
            throw new InvalidOperationException("Instance already exists");
        ++s_instanceCount;

        _terminal = Terminal.System;
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
        _cb.SetScreenBuffer(ScreenBuffer.Alternate);
        OutClearScreen();
        OutFlush();

        Terminal.Signaled += OnSignaled;
        Terminal.Resized += OnResized;

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

    public void GetEvents(IList<IEvent> events)
    {
        OutFlush();

        // block on the first one
        events.Add(_events.Receive());

        // receive as many more as we can
        while (_events.TryReceive(out var evt))
            events.Add(evt);
    }

    public TerminalSize Size => _terminal.Size;

    public void Dispose()
    {
        if (_disposed)
            throw new ObjectDisposedException("Instance already disposed");

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
        Terminal.Resized -= OnResized;

        _cb.Clear(); // drop anything that was in progress
        OutResetAttributes();
        OutClearScreen();
        _cb.SetScreenBuffer(ScreenBuffer.Main);
        OutShowCursor(true);
        OutFlush();

        Terminal.DisableRawMode();

        _disposed = true;
        --s_instanceCount;
    }

    void OnSignaled(TerminalSignalContext signal)
    {
        _events.Post(new SignalEvent(signal.Signal));
    }

    void OnResized(TerminalSize size)
    {
        _events.Post(new ResizeEvent(size));
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

        while (!_disposed)
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
        while (!_disposed)
            parser.Process();
    }
}
