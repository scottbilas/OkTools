using System.Threading.Channels;
using OkTools.Core.Terminal;
using Spectre.Console;
using Vezel.Cathode;
using Vezel.Cathode.Text.Control;
using Color = Spectre.Console.Color;

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

class StaleApp : IDisposable
{
    readonly Context _ctx;
    readonly StaleOptions _options;
    readonly ChildProcess _process;

    readonly Screen _screen = new(Terminal.System);
    readonly StatusPane _topStatusPane, _bottomStatusPane;
    readonly Channel<KeyEvent> _events = Channel.CreateUnbounded<KeyEvent>();

    int _pauseSkip;

    // TODO: invalidate all panels on resize

    StaleApp(Context ctx, StaleOptions options, ChildProcess process)
    {
        _ctx = ctx; // unowned
        _options = options;
        _process = process;

        _topStatusPane = new StatusPane(_screen, 0, Constants.TopStatusColor);
        _bottomStatusPane = new StatusPane(_screen, 0, Constants.BottomStatusColor);
        _pauseSkip = _options.PauseEveryLine ? 0 : -1;
    }

    void IDisposable.Dispose()
    {
        _screen.Control.Clear();
        _screen.Control.ResetScrollMargin();
        _screen.Control.MoveCursorTo(_bottomStatusPane.Top, 0);
        _screen.Control.PrintLine();

        if (_ctx.IsCancellationRequested)
            _screen.Control.PrintLine("User aborted!", k_userAlertStyle);

        _screen.FlushStdout();

        if (_ctx.IsCancellationRequested)
            _ctx.LongTasks.AbortAll();

        _screen.Dispose();
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

        Terminal.EnableRawMode();

        // fire & forget, because ReadKeysAsync routes them into the channel and the `await foreach` will pick that up
        _ctx.LongTasks.RunWeak("Stdin Reader", cancel => AnsiInput.ReadKeysAsync(Terminal.TerminalIn, _events.Writer, e =>
        {
            if (e is { Modifiers: ConsoleModifiers.Control, Key: ConsoleKey.C })
                _ctx.Cancel();
            return e;
        }, cancel));

        if (_options.Start != null)
        {
            _screen.Control.ClearScreen();
            _screen.SetCursorPos(0, _options.Start.Value);
            await _screen.FlushStdoutAsync(_ctx.CancelToken);
        }

        _topStatusPane.PrintLine(GetTopStatusText());
        ControlPrintEndOfText(true);
        _bottomStatusPane.Print(GetBottomStatusText());
        _screen.Control.MoveCursorLineStart();
        _screen.Control.MoveCursorUp();

        var cursorPos = _screen.GetCursorPos();
        _topStatusPane.Top = cursorPos.Y-1;
        _bottomStatusPane.Top = cursorPos.Y+1;

        //// main loop

        await foreach (var capture in _process.Captures.ReadAllAsync(_ctx.CancelToken))
        {
            // TODO: support incremental printing per line

            if (_ctx.IsCancellationRequested)
                break;

            if (!capture.Line.IsNullOrWhiteSpace())
            {
                // TODO: line continuation markup, wrap/truncate handling, etc.

                var chars = capture.Line.AsMemory();
                while (chars.Length > _screen.ScreenWidth && !_ctx.IsCancellationRequested)
                {
                    await OutLineAsync(capture.IsStdErr, chars[.._screen.ScreenWidth]);
                    chars = chars[_screen.ScreenWidth..];
                }

                if (chars.Length > 0)
                    await OutLineAsync(capture.IsStdErr, chars);
            }
            else
            {
                // TODO: consider a flag to skip these or a sequence of n of these (to compress vertical space a bit where we have a spacey tool).
                // could also consider compressing them after seeing a few lines worth (with a little margin marker)

                await OutLineAsync(capture.IsStdErr, default);
            }
        }

        if (_process.Exited.IsCompleted && !_ctx.CancelToken.IsCancellationRequested)
            await _ctx.OutMarkupInterpLineAsync($"[italic]{_process.Command} exited normally with exit code {_process.Exited.Result}[/]");

        return CliExitCode.Success;
    }

    string GetTopStatusText() => $">{_process.Id} $ {_process.Command} {CliUtility.CommandLineArgsToString(_process.Args)}";
    string GetBottomStatusText() => $"...top={_topStatusPane.Top}, btm={_bottomStatusPane.Top}";

    static readonly Style k_stdErrStyle = new(background: Color.DarkRed);
    static readonly Style k_userAlertStyle = new(foreground: Color.Yellow);

    void ControlFinishLogLine(bool newline)
    {
        _screen.Control.ClearLine(ClearMode.After);
        if (newline)
            _screen.Control.PrintLine();
        else
            _screen.Control.MoveCursorLineStart();
    }

    void ControlPrintLogLine(bool isStdErr, ReadOnlyMemory<char> chars, bool newline)
    {
        if (isStdErr)
            _screen.Control.Print(chars.ToString(), k_stdErrStyle);
        else
            _screen.Control.Print(chars.Span);
        ControlFinishLogLine(newline);
    }

    void ControlPrintLogLine(bool isStdErr, string text, bool newline)
    {
        if (isStdErr)
            _screen.Control.Print(text, k_stdErrStyle);
        else
            _screen.Control.Print(text);
        ControlFinishLogLine(newline);
    }

    // TODO: spinner + timer while child process still running
    void ControlPrintEndOfText(bool newline) => ControlPrintLogLine(false, "‡", newline);

    async ValueTask OutLineAsync(bool isStdErr, ReadOnlyMemory<char> chars)
    {
        Pause();

        // check for ctrl-c with peek (need to buffer stdin on my own)

        if (chars.Length > _screen.ScreenWidth)
            throw new ArgumentException($"Too wide ({chars.Length}) for screen width ({_screen.ScreenWidth})");

        if (_ctx.IsCancellationRequested)
            return;

        // TODO: support batching output. for example shouldn't redraw status except every x msec or keep issuing unnecessary margin updates.

        var belowTop = _topStatusPane.Top > 0; // allow top status to scroll up without going offscreen
        var aboveBottom = _bottomStatusPane.Top < _screen.ScreenHeight-1;

        // not yet at bottom; push lower status down
        if (aboveBottom)
        {
            _screen.Control.SetScrollMargin(_bottomStatusPane.Top, _screen.ScreenHeight-1);
            _screen.Control.MoveBufferDown();
            ++_bottomStatusPane.Top;
        }

        if (belowTop || aboveBottom)
        {
            _screen.Control.SetScrollMargin(belowTop ? 0 : 1, _bottomStatusPane.Top-1);
            _screen.Control.MoveCursorTo(_bottomStatusPane.Top - (aboveBottom ? 2 : 1), 0);
        }

        ControlPrintLogLine(isStdErr, chars, true);
        ControlPrintEndOfText(false);

        // only if we pushed the top status up
        if (!aboveBottom && belowTop)
        {
            // only if we finally hit the very top
            if (--_topStatusPane.Top == 0)
            {
                // set the scroll region and cursor to the full area just once, and we'll leave it alone after that
                _screen.Control.SetScrollMargin(1, _bottomStatusPane.Top-1);
                _screen.Control.MoveCursorTo(_bottomStatusPane.Top-1, 0);
            }
        }

        _bottomStatusPane.Update(GetBottomStatusText());

        await _screen.FlushStdoutAsync(_ctx.CancelToken);
    }

    // ReSharper disable once UnusedMember.Local
    void DebugRenderCursorPos()
    {
        {
            using var _ = _screen.Control.AutoSaveRestoreCursorState();
            var pos = _screen.GetCursorPos();
            var text = $"     {pos.X},{pos.Y}";
            _screen.SetCursorPos(_screen.ScreenWidth - text.Length, 0);
            _screen.Control.Print(text);
        }

        _screen.FlushStdout();
    }

    void Pause()
    {
        if (_pauseSkip != 0)
        {
            if (_pauseSkip > 0)
                --_pauseSkip;
            return;
        }

        while (!_ctx.CancelToken.IsCancellationRequested)
        {
            if (!_events.Reader.TryRead(out var evt))
            {
                Thread.Sleep(10);
                continue;
            }

            if (evt is { Char: 'q', Modifiers: 0 })
                throw new CliExitException("User quit");

            if (evt is { Char: '\r', Modifiers: 0 })
                _pauseSkip = 0;
            else if (evt is { Char: ' ', Modifiers: 0 })
                _pauseSkip = 9;
            else
                continue;

            break;
        }
    }
}
