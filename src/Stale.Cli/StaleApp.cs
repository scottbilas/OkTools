using System.Threading.Channels;
using OkTools.Core.Terminal;
using Vezel.Cathode;

readonly record struct StaleChildProcess(
    string Command, IReadOnlyList<string> Args,
    ChannelReader<LineCapture> Captures,
    int Id, Task<int> Exited);

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
    readonly Screen _screen = new(Terminal.System);
    readonly StatusPane _topStatusPane, _bottomStatusPane;

    // TODO: invalidate all panels on resize

    public StaleApp(Context ctx)
    {
        _ctx = ctx; // unowned
        _topStatusPane = new StatusPane(_screen, 0, Constants.TopStatusColor);
        _bottomStatusPane = new StatusPane(_screen, 0, Constants.BottomStatusColor);
    }

    void IDisposable.Dispose()
    {
        _screen.Dispose();
        if (_bottomStatusPane.Top != 0)
        {
            _screen.Control.Clear();
            _screen.Control.MoveCursorTo(_bottomStatusPane.Top, 0);
            _screen.Control.PrintLine();
            _screen.FlushStdout();
        }
    }

    public async Task<CliExitCode> Run(StaleOptions staleOptions, StaleChildProcess process)
    {
        string GetTopStatusText() => $">{process.Id} $ {process.Command} {CliUtility.CommandLineArgsToString(process.Args)}";
        string GetBottomStatusText() => $"...top={_topStatusPane.Top}, btm={_bottomStatusPane.Top}, cursor={_screen.GetCursorPos()}";

        //// setup

        Terminal.EnableRawMode();

        // fire & forget, because ReadKeysAsync routes them into the channel and the `await foreach` will pick that up
        var events = Channel.CreateUnbounded<KeyEvent>();
        _ctx.LongTasks.Run("Stdin Reader", cancel => AnsiInput.ReadKeysAsync(Terminal.TerminalIn, events.Writer, e =>
        {
            if (e is { Modifiers: ConsoleModifiers.Control, Key: ConsoleKey.C })
                _ctx.Cancel();
            return e;
        }, cancel));

        if (staleOptions.Start != null)
        {
            _screen.Control.ClearScreen();
            _screen.SetCursorPos(0, staleOptions.Start.Value);
            _screen.FlushStdout();
        }

        _topStatusPane.Print(GetTopStatusText());
        _screen.Control.PrintLine();
        _screen.Control.PrintLine("‡");
        _bottomStatusPane.Print(GetBottomStatusText());
        _screen.Control.MoveCursorLineStart();
        _screen.Control.MoveCursorUp();

        var cursorPos = _screen.GetCursorPos();
        _topStatusPane.Top = cursorPos.Y-1;
        _bottomStatusPane.Top = cursorPos.Y+1;

        //// utility

        var pauseSkip = _ctx.Options.OptPause ? 0 : -1;

        void Pause()
        {
            if (pauseSkip != 0)
            {
                if (pauseSkip > 0)
                    --pauseSkip;
                return;
            }

            for (;;)
            {
                var key = new byte[1];
                Terminal.Read(key);  // TODO: copy pasta rotate InputParser from Flog

                switch ((char)key[0])
                {
                    case 'q' : _ctx.Cancel(); break;
                    case '\r': pauseSkip = 0; break;
                    case ' ' : pauseSkip = 9; break;
                    default  : continue;
                }

                break;
            }
        }

        void OutLine(bool isStdErr, ReadOnlySpan<char> span)
        {
            Pause();

            // check for ctrl-c with peek (need to buffer stdin on my own)

            if (span.Length > _screen.ScreenWidth)
                throw new ArgumentException($"Span too wide ({span.Length}) for screen width ({_screen.ScreenWidth})");

            if (_ctx.IsCancellationRequested)
                return;

            // pre-scroll
            if (_bottomStatusPane.Top < _screen.ScreenHeight-1)
            {
                {
                    using var _ = _screen.SaveRestoreCursorPos(flushOnDispose: true);
                    _screen.Control.SetScrollMargin(_bottomStatusPane.Top-1, _screen.ScreenHeight-1);
                    _screen.Control.MoveBufferDown();
                    _screen.Control.ResetScrollMargin();
                }

                if (span.Length == 0)
                {
                    _screen.Control.PrintLine(" "); // overwrite dagger
                }
                else if (isStdErr)
                {
                    _screen.Control.MoveCursorLineStart();
                    _screen.Control.PrintMarkupLineInterp($"[default on darkred]{span.ToString()}[/]");
                }
                else
                {
                    _screen.Control.PrintLine(span);
                }
                ++cursorPos.Y;

                ++_bottomStatusPane.Top;
                _bottomStatusPane.Update(GetBottomStatusText());
            }
/*            else if (top > 0)
            {
                cb.SetScrollMargin(0, btm - btmStatusHeight);
                cb.MoveBufferUp(1);
                --top;
            }
            else
            {
                cb.SetScrollMargin(topStatusHeight, btm-btmStatusHeight);
                cb.MoveBufferUp(1);
            }*/

//            cb.SetScrollMargin(0, _screen.ScreenHeight-1);
//            cb.MoveCursorTo(btm-btmStatusHeight, 0);
//            FlushCb();

            // TODO: should i be using async versions of these Out funcs..?
//            if (isStdErr)
//                _ctx.MarkupLine($"[{stderrColor}]{span.ToString().PadRight(_screen.ScreenWidth).EscapeMarkup()}[/]");
//            else
//                _ctx.Line(span);

//            UpdateBtmStatus();

            _screen.FlushStdout();
        }

        //// main loop

        await foreach (var capture in process.Captures.ReadAllAsync(_ctx.CancelToken))
        {
            // TODO: support incremental printing per line
            // TODO: support batching output. shouldn't redraw status except every x msec.

            // need this local function to work around dotnet await+span limitation
            void Process()
            {
                var span = capture.Line.AsSpan();

                while (span.Length > _screen.ScreenWidth && !_ctx.IsCancellationRequested)
                {
                    OutLine(capture.IsStdErr, span[.._screen.ScreenWidth]);
                    span = span[_screen.ScreenWidth..];
                }

                if (span.Length > 0)
                    OutLine(capture.IsStdErr, span);
            }

            if (_ctx.IsCancellationRequested)
                break;

            if (capture.Line.IsNullOrWhiteSpace())
            {
                // TODO: consider a flag to skip these or a sequence of n of these (to compress vertical space a bit where we have a spacey tool).
                // could also consider compressing them after seeing a few lines worth (with a little margin marker)

                OutLine(capture.IsStdErr, default);
            }
            else
                Process();
        }

        //// cleanup

        _ctx.OutLine();

        if (_ctx.IsCancellationRequested)
            _ctx.OutMarkupLine("[yellow]Aborted![/]");

        if (process.Exited.IsCompleted)
            _ctx.OutLine($"{process.Command} exited normally with exit code {process.Exited.Result}");

        return CliExitCode.Success;
    }
}
