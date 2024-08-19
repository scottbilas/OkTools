using System.Drawing;
using System.Threading.Channels;
using OkTools.Core.Terminal;
using Spectre.Console;
using Vezel.Cathode.Text.Control;
using Color = Spectre.Console.Color;
using Size = System.Drawing.Size;

class CliExitException : Exception
{
    public CliExitException(string message)
        : base(message) {}
}

readonly struct StaleOptions
{
    public StaleOptions(StaleCliArguments args)
    {
        if (args.OptStart != null)
        {
            if (!int.TryParse(args.OptStart, out var start))
                throw new CliErrorException(CliExitCode.ErrorUsage, "START must be an integer");
            Start = start;
        }
        PauseEveryLine = args.OptPause;
    }

    public readonly int? Start;
    public readonly bool PauseEveryLine;
}

record ResizeEvent(Size NewSize) : ITerminalEvent;

class StaleApp : IDisposable
{
    bool _disposed;

    readonly Context _ctx;
    readonly StaleOptions _options;

    readonly ControlBuilder _cb = new();
    Size _screenSize;
    readonly StatusPane _topStatusPane, _bottomStatusPane;

    readonly ChildProcess _process;
    readonly Channel<ITerminalEvent> _userEvents = Channel.CreateUnbounded<ITerminalEvent>();
    int _pauseSkip;

    StaleApp(Context ctx, StaleOptions options, ChildProcess process)
    {
        _ctx = ctx; // unowned
        _options = options;
        _screenSize = _ctx.Terminal.Size;
        _process = process;

        _topStatusPane = new StatusPane(0, _screenSize.Width, Constants.TopStatusColor);
        _bottomStatusPane = new StatusPane(0, _screenSize.Width, Constants.BottomStatusColor);
        _pauseSkip = _options.PauseEveryLine ? 0 : -1;

        //$$$
        _ctx.Terminal.Resized += OnResized;

        _cb.SoftReset();
        _cb.HideCursor();
    }

    void OnResized(Size newSize) => _userEvents.Writer.TryWrite(new ResizeEvent(newSize)); // unbounded channel will only fail to write when channel is completed

    void IDisposable.Dispose()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _disposed = true;

        _ctx.Terminal.Resized -= OnResized;

        _cb.Clear();
        _cb.SoftReset(); // set terminal back to normal, we may have leftover state like margins or whatever
        if (_bottomStatusPane.Top != 0)
            _cb.MoveCursorTo(_bottomStatusPane.Top, 0);
        _cb.PrintLine();

        if (_ctx.IsCancellationRequested)
            _cb.PrintLine("User aborted!", k_userAlertStyle);

        FlushStdout();

        if (_ctx.IsCancellationRequested)
            _ctx.LongTasks.AbortAll();
    }

    public static async Task<CliExitCode> Run(Context ctx, StaleOptions options, ChildProcess process)
    {
        var result = CliExitCode.ErrorGeneral;

        try
        {
            using var app = new StaleApp(ctx, options, process);
            result = await app.RunInternal();
        }
        catch (OperationCanceledException) { /* ok */ }

        if (ctx.IsCancellationRequested)
            result = UnixSignal.KeyboardInterrupt.AsCliExitCode();

        return result;
    }

    async Task<CliExitCode> RunInternal()
    {
        //// setup

        _ctx.Terminal.EnableRawMode();

        // fire & forget, because ReadKeysAsync routes them into the channel and the `await foreach` will pick that up
        _ctx.LongTasks.RunWeak("Stdin Reader", cancel => AnsiInput.ReadKeysAsync(_ctx.Terminal.TerminalIn, _userEvents.Writer, e =>
        {
            if (e is { Modifiers: ConsoleModifiers.Control, Key: ConsoleKey.C })
                _ctx.Cancel();
            return e;
        }, cancel));

        if (_options.Start != null)
        {
            _cb.ClearScreen();
            SetCursorPos(_options.Start.Value, 0);
            await FlushStdoutAsync();
        }

        _topStatusPane.PrintLine(_cb, GetTopStatusText());
        ControlPrintEndOfText(true);
        _bottomStatusPane.Print(_cb, GetBottomStatusText());
        _cb.MoveCursorLineStart();
        _cb.MoveCursorUp();

        var cursorPos = GetCursorPos();
        _topStatusPane.Top = cursorPos.Y-1;
        _bottomStatusPane.Top = cursorPos.Y+1;

        //// main loop

        var captures = new LineCapture[1000];
        var captureCount = 0;
        while (!_ctx.IsCancellationRequested)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            while (_userEvents.Reader.TryRead(out var userEvent))
            {
                switch (userEvent)
                {
                    case ResizeEvent resizeEvent:
                        HandleResize(resizeEvent);
                        break;
                    case KeyEvent { Char: 'q', Modifiers: 0 }:
                        HandleUserQuit();
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown event type {userEvent.GetType()}");
                }
            }

            while (_process.Captures.TryRead(out var capture))
            {
                captures[captureCount++] = capture;

                // if flooding, give a chance to do a render and deal with user input periodically
                if (captureCount == captures.Length)
                    break;
            }

            if (captureCount != 0)
            {
                await LogCapturesAsync(captures.WithLength(captureCount));
                captureCount = 0;
            }
            else if (await Task.WhenAny(
                _process.Exited,
                _userEvents.Reader.WaitToReadAsync(_ctx.CancelToken).AsTask(),
                _process.Captures.WaitToReadAsync(_ctx.CancelToken).AsTask()) == _process.Exited)
            {
                // TODO: consider whether to stay running idle even after process completed, so as to permit the nice scrollback features

                ControlPrintLogLine(OutLineType.Status, $"{_process.Command} exited normally with exit code {_process.Exited.Result}", false);
                await FlushStdoutAsync();
                break;
            }
        }

        return CliExitCode.Success;
    }

    string GetTopStatusText() => $">{_process.Id} $ {_process.Command} {CliUtility.CommandLineArgsToString(_process.Args)}";
    string GetBottomStatusText() => $"...top={_topStatusPane.Top}, btm={_bottomStatusPane.Top}";

    static readonly Style k_stdErrStyle = new(background: Color.DarkRed);
    static readonly Style k_statusStyle = new(decoration: Decoration.Italic);
    static readonly Style k_userAlertStyle = new(foreground: Color.Yellow);
    static readonly Style k_endOfTextStyle = new(foreground: Color.Grey);

    void ControlFinishLogLine(bool newline)
    {
        _cb.ClearLine(ClearMode.After);
        if (newline)
            _cb.PrintLine();
        else
            _cb.MoveCursorLineStart();
    }

    void ControlPrintLogLine(OutLineType lineType, ReadOnlyMemory<char> chars, bool newline)
    {
        switch (lineType)
        {
            case OutLineType.CapturedStdout:
                _cb.Print(chars.Span);
                break;
            case OutLineType.CapturedStderr:
                _cb.Print(chars.ToString(), k_stdErrStyle);
                break;
            case OutLineType.Status:
                _cb.Print(chars.ToString(), k_statusStyle);
                break;
            case OutLineType.EndOfText:
                _cb.Print(chars.ToString(), k_endOfTextStyle);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lineType));
        }
        ControlFinishLogLine(newline);
    }

    void ControlPrintLogLine(OutLineType lineType, string text, bool newline) =>
        ControlPrintLogLine(lineType, text.AsMemory(), newline);

    // TODO: spinner + timer while child process still running
    void ControlPrintEndOfText(bool newline) =>
        ControlPrintLogLine(OutLineType.EndOfText, _options.PauseEveryLine ? "‡ " + k_pauseText : "‡", newline);

    enum OutLineType { CapturedStdout, CapturedStderr, Status, EndOfText }

    ValueTask OutLineAsync(bool stdErrOrOut, ReadOnlyMemory<char> chars) =>
        OutLineAsync(stdErrOrOut ? OutLineType.CapturedStderr : OutLineType.CapturedStdout, chars);

    async ValueTask OutLineAsync(OutLineType lineType, ReadOnlyMemory<char> chars)
    {
        await LinePauseAsync();

        // check for ctrl-c with peek (need to buffer stdin on my own)

        if (chars.Length > _screenSize.Width)
            throw new ArgumentException($"Too wide ({chars.Length}) for screen width ({_screenSize.Width})");

        if (_ctx.IsCancellationRequested)
            return;

        // TODO: support batching output. for example shouldn't redraw status except every x msec or keep issuing unnecessary margin updates.

        var belowTop = _topStatusPane.Top > 0; // allow top status to scroll up without going offscreen
        var aboveBottom = _bottomStatusPane.Top < _screenSize.Height-1;

        // not yet at bottom; push lower status down
        if (aboveBottom)
        {
            _cb.SetScrollMargin(_bottomStatusPane.Top, _screenSize.Height-1);
            _cb.MoveBufferDown();
            ++_bottomStatusPane.Top;
        }

        if (belowTop || aboveBottom)
        {
            _cb.SetScrollMargin(belowTop ? 0 : 1, _bottomStatusPane.Top-1);
            _cb.MoveCursorTo(_bottomStatusPane.Top - (aboveBottom ? 2 : 1), 0);
        }

        ControlPrintLogLine(lineType, chars, true);
        ControlPrintEndOfText(false);

        // only if we pushed the top status up
        if (!aboveBottom && belowTop)
        {
            // only if we finally hit the very top
            if (--_topStatusPane.Top == 0)
            {
                // set the scroll region and cursor to the full area just once, and we'll leave it alone after that
                _cb.SetScrollMargin(1, _bottomStatusPane.Top-1);
                _cb.MoveCursorTo(_bottomStatusPane.Top-1, 0);
            }
        }

        _bottomStatusPane.Update(_cb, GetBottomStatusText());

        await FlushStdoutAsync();
    }

    async Task LogCapturesAsync(ArraySegment<LineCapture> captures)
    {
        // TODO: process captures as a single batch

        foreach (var capture in captures)
        {
            // TODO: support incremental printing per line

            if (_ctx.IsCancellationRequested)
                break;

            if (!capture.Line.IsNullOrWhiteSpace())
            {
                // TODO: line continuation markup, wrap/truncate handling, etc.

                var chars = capture.Line.AsMemory();
                while (chars.Length > _screenSize.Width && !_ctx.IsCancellationRequested)
                {
                    await OutLineAsync(capture.IsStdErr, chars[.._screenSize.Width]);
                    chars = chars[_screenSize.Width..];
                }

                if (chars.Length > 0)
                    await OutLineAsync(capture.IsStdErr, chars);
            }
            else
            {
                // TODO: consider a flag to skip these or a sequence of n of these (to compress vertical space a bit where we have a spacey tool).
                // could also consider compressing them after seeing a few lines worth (with a little margin marker)

                await OutLineAsync(capture.IsStdErr, default(ReadOnlyMemory<char>));
            }
        }
    }

    const string k_pauseText = "(paused: enter/space to advance 1/10; q to quit)";

    async Task LinePauseAsync()
    {
        if (_pauseSkip != 0)
        {
            if (_pauseSkip > 0)
                --_pauseSkip;
            return;
        }

        while (!_ctx.CancelToken.IsCancellationRequested)
        {
            var userEvent = await _userEvents.Reader.ReadAsync(_ctx.CancelToken);
            switch (userEvent)
            {
                case ResizeEvent resizeEvent:
                    HandleResize(resizeEvent);
                    break;

                case KeyEvent keyEvent:
                    if (keyEvent is { Char: 'q', Modifiers: 0 })
                        HandleUserQuit();

                    if (keyEvent is { Char: '\r', Modifiers: 0 })
                        _pauseSkip = 0;
                    else if (keyEvent is { Char: ' ', Modifiers: 0 })
                        _pauseSkip = 9;
                    else
                        continue;
                    break;

                default:
                    throw new InvalidOperationException($"Unknown event type {userEvent.GetType()}");
            }

            break;
        }
    }

    // ReSharper disable once UnusedMember.Local
    void DebugRenderCursorPos()
    {
        {
            using var _ = _cb.SaveRestoreCursor();
            var pos = GetCursorPos();
            var text = $"     {pos.X},{pos.Y}";
            SetCursorPos(text.Length - _screenSize.Width, 0);
            _cb.Print(text);
        }

        FlushStdout();
    }

    void FlushStdout()
    {
        if (_cb.Span.IsEmpty)
            return;

        _ctx.Terminal.Out(_cb.Span);
        _cb.Clear(10*1024);
    }

    async ValueTask FlushStdoutAsync()
    {
        if (_cb.Span.IsEmpty)
            return;

        await _ctx.Terminal.OutAsync(_cb.Memory, _ctx.CancelToken);
        _cb.Clear(10*1024);
    }

    void SetCursorPos(int y, int x)
    {
        if (x < 0 || x >= _screenSize.Width)
            throw new ArgumentOutOfRangeException(nameof(x), $"Out of range 0 <= {x} < {_screenSize.Width}");
        if (y < 0 || y >= _screenSize.Height)
            throw new ArgumentOutOfRangeException(nameof(y), $"Out of range 0 <= {y} < {_screenSize.Height}");

        _cb.MoveCursorTo(y, x);
    }

    static void HandleUserQuit()
    {
        throw new CliExitException("User quit");
    }

    void HandleResize(ResizeEvent resizeEvent)
    {
        _screenSize = resizeEvent.NewSize;
        _topStatusPane.MaxWidth = _screenSize.Width;
        _topStatusPane.Invalidate();
        _bottomStatusPane.MaxWidth = _screenSize.Width;
        _bottomStatusPane.Invalidate();

        // TODO: react to shorter height
        // TODO: re-render logview?
    }

#   pragma warning disable CA1822, RS0030
    Point GetCursorPos()
    {
        // may be lingering move ops
        FlushStdout();

        // cathode does not yet support reading cursor position. see https://github.com/vezel-dev/cathode/discussions/16
        // i get the concern, but for this app i'm ensuring that i do cursor reading at well-defined times.
        var (left, top) = Console.GetCursorPosition();
        return new Point(left, top);
    }
#   pragma warning restore CA1822, RS0030
}
