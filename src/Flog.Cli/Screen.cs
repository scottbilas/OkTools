using System.Threading.Channels;

partial class Screen : IDisposable
{
    static int s_instanceCount;
    readonly CancellationTokenSource _disposed = new();
    readonly VirtualTerminal _terminal;

    readonly Channel<ITerminalEvent> _terminalEvents = Channel
        .CreateUnbounded<ITerminalEvent>(new UnboundedChannelOptions
            { SingleReader = true }); // only mainloop processes incoming events

    public Screen()
    {
        if (s_instanceCount > 0)
            throw new InvalidOperationException("Instance already exists");
        ++s_instanceCount;

        _terminal = Terminal.System;
        if (!Terminal.StandardIn.IsInteractive)
            throw new CliExitException("This app requires an interactive terminal", CliExitCode.ErrorUsage);

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

        #if ENABLE_SCREEN_RECORDER
        _recorder.OnResized(_terminal.Size);
        #endif

        Terminal.EnableRawMode();
        _cb.SetScreenBuffer(ScreenBuffer.Alternate);
        OutClearScreen();
        OutFlush();

        Terminal.Signaled += OnSignaled;
        Terminal.Resized += OnResized;

        // initial size event to kick off a layout and refresh
        OnResized(Terminal.Size);

        async void Wrap(Action action)
        {
            try
            {
                action();
            }
            catch (Exception x)
            {
                await _terminalEvents.Writer.WriteAsync(new ErrorEvent(x), _disposed.Token);
            }
        }

        var terminalRawInput = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

        // TODO: switch terminalRawInput from a Channel to a Pipe. should simplify the state machine.
        Task.Run(() => Wrap(() => TaskReadRawInput(terminalRawInput.Writer)));
        Task.Run(() => Wrap(() => TaskReadKeyEvents(terminalRawInput.Reader)));
    }

    public async void PostEvent(ITerminalEvent evt) => await _terminalEvents.Writer.WriteAsync(evt, _disposed.Token);

    public TerminalSize Size => _terminal.Size;
    public ChannelReader<ITerminalEvent> Events => _terminalEvents.Reader;
    public Options Options { get; } = new();

    #if ENABLE_SCREEN_RECORDER
    public ScreenRecorder Recorder => _recorder;
    #endif

    public void Dispose()
    {
        if (_disposed.IsCancellationRequested)
            throw new ObjectDisposedException("Instance already disposed");
        _disposed.Cancel(); // don't dispose, let finalizer get it

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

        --s_instanceCount;
    }

    void OnSignaled(TerminalSignalContext signal) => PostEvent(new SignalEvent(signal.Signal));

    void OnResized(TerminalSize size)
    {
        PostEvent(new ResizeEvent(size));

        #if ENABLE_SCREEN_RECORDER
        _recorder.OnResized(size);
        #endif
    }

    async void TaskReadRawInput(ChannelWriter<ReadOnlyMemory<byte>> rawInput)
    {
        // vezel cathode doesn't support cancelable key read on windows, so we have to roll our own
        //
        // see:
        //
        //  * https://github.com/vezel-dev/cathode/issues/63 (Support input cancellation on Windows)
        //  * https://github.com/vezel-dev/cathode/issues/59 (Implement a control sequence parser)
        //

        // TODO: would really like to use a cancelable Terminal.Read or use a Peek() on there instead, so we can
        // get rid of this unnecessary extra _rawInput protocol (needs a timeout in order to detect standalone ESC).
        // (keep the combined read+process in a task off the main thread, though.)

        // TODO: turn this into async enumerable (??)
        while (!_disposed.IsCancellationRequested)
        {
            var input = new byte[10];
            var read = await Terminal.ReadAsync(input, _disposed.Token);
            if (read == 0)
            {
                await _terminalEvents.Writer.WriteAsync(new SignalEvent(TerminalSignal.Close), _disposed.Token);
                break;
            }

            await rawInput.WriteAsync(new Memory<byte>(input, 0, read), _disposed.Token);
        }
    }

    async void TaskReadKeyEvents(ChannelReader<ReadOnlyMemory<byte>> rawInput)
    {
        var parser = new InputParser(rawInput, _terminalEvents);
        while (!_disposed.IsCancellationRequested)
            await parser.Process();
    }
}
