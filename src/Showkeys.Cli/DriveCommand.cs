using System.Drawing;
using System.Text;
using System.Threading.Channels;
using OkTools.Core.Terminal;
using Vezel.Cathode;
using Vezel.Cathode.Text.Control;

// ReSharper disable AccessToModifiedClosure

class DriveCommand
{
    readonly ControlBuilder _cb = new(100);
    readonly StringBuilder _sb = new();
    int _pattern;
    Size _terminalSize;
    int _scrollTop, _scrollBottom;
    Point _cursorPos;
    CursorConstrainMode _cursorConstrainMode;

    public async Task<CliExitCode> RunAsync(bool save)
    {
        Terminal.EnableRawMode();
        using var __ = save ? new AlternateScreen(OutControl) : (IDisposable?)null;

        _terminalSize = Terminal.Size;
        _scrollTop = 0;
        _scrollBottom = _terminalSize.Height - 1;

        OutLineRaw($"Terminal size: {_terminalSize.Width}x{_terminalSize.Height}");
        Help();
        PrintStatus("");

        var events = Channel.CreateUnbounded<IEvent>();

        // fire & forget, because ReadKeysAsync routes them into the channel and the `await foreach` will pick that up
        _ = Task.Run(() => AnsiInput.ReadKeysAsync(Terminal.TerminalIn, events.Writer, e => new InputEvent(e)));

        Terminal.Resized += newSize => events.Writer.TryWrite(new TerminalSizeEvent(newSize));

        await foreach (var evt in events.Reader.ReadAllAsync())
        {
            if (evt is TerminalSizeEvent sizeEvent)
            {
                // keep scroll bottom at end if we had that previously, otherwise clamp to new max
                var newBottom = _scrollBottom;
                if (_scrollBottom == _terminalSize.Height-1 || _scrollBottom >= sizeEvent.NewSize.Height)
                    newBottom = sizeEvent.NewSize.Height-1;

                // maintain scroll margin height if we can
                var newTop = _scrollTop;
                if (_scrollTop >= newBottom)
                    newTop = Math.Max(0, newBottom - (_scrollBottom - _scrollTop));

                // now we can commit the size (do this before SetScrollMargin)
                _terminalSize = sizeEvent.NewSize;

                SetScrollMargin(newTop, newBottom, out var _);
                OutLineRaw($"Terminal resized: {_terminalSize.Width}x{_terminalSize.Height}");
                PrintStatus("");

                continue;
            }

            var keyEvent = ((InputEvent)evt).KeyEvent;
            var error = "";

            switch (keyEvent)
            {
                // fills

                case { Char: 'f', Modifiers: 0 }:
                    OutControl(cb => cb.SaveCursorState());
                    PrintPatternLines(_scrollBottom - _scrollTop + 1);
                    OutControl(cb => cb.RestoreCursorState());
                    if (++_pattern == 1)
                        _pattern = 0;
                    break;

                case { Key: var k and >= ConsoleKey.F1 and <= ConsoleKey.F12, Modifiers: 0 }:
                    PrintPatternLines(k - ConsoleKey.F1 + 1);
                    break;

                case { Char: var c and >= '1' and <= '9', Modifiers: 0 }:
                    PrintPatternWords(c - '0');
                    break;

                case { Char: var c and >= '1' and <= '9', Modifiers: ConsoleModifiers.Alt }:
                    PrintRandomChars(c - '0');
                    break;

                case { Char: 'h' or '?', Modifiers: 0 }:
                    Help();
                    break;

                // moves

                case { Key: ConsoleKey.LeftArrow, Modifiers: 0 }:
                    OutControl(cb => cb.MoveCursorLeft());
                    break;
                case { Key: ConsoleKey.RightArrow, Modifiers: 0 }:
                    OutControl(cb => cb.MoveCursorRight());
                    break;
                case { Key: ConsoleKey.UpArrow, Modifiers: 0 }:
                    OutControl(cb => cb.ReverseLineFeed()); // MoveCursorDown/Up does not scroll
                    break;
                case { Key: ConsoleKey.DownArrow, Modifiers: 0 }:
                    OutControl(cb => cb.LineFeed()); // MoveCursorDown/Up does not scroll
                    break;
                case { Key: ConsoleKey.UpArrow, Modifiers: ConsoleModifiers.Shift }:
                    OutControl(cb => cb.MoveCursorTo((_cursorPos.Y-1).ClampMin(0), _cursorPos.X));
                    break;
                case { Key: ConsoleKey.DownArrow, Modifiers: ConsoleModifiers.Shift }:
                    OutControl(cb => cb.MoveCursorTo((_cursorPos.Y+1).ClampMaxExcl(_terminalSize.Height), _cursorPos.X));
                    break;

                case { Key: ConsoleKey.Home, Modifiers: 0 }:
                    OutControl(cb => cb.MoveCursorHomeLine());
                    break;
                case { Key: ConsoleKey.Home, Modifiers: ConsoleModifiers.Control }:
                    OutControl(cb => cb.MoveCursorHome());
                    break;

                case { Key: ConsoleKey.End, Modifiers: 0 }:
                    OutControl(cb => cb.MoveCursorEndLine());
                    break;
                case { Key: ConsoleKey.End, Modifiers: ConsoleModifiers.Control }:
                    OutControl(cb => cb.MoveCursorEnd());
                    break;

                case { Key: ConsoleKey.Enter, Modifiers: 0 }:
                    OutControl(cb => cb.CarriageReturn().LineFeed());
                    break;

                case { Key: ConsoleKey.UpArrow, Modifiers: ConsoleModifiers.Control }:
                    OutControl(cb => cb.MoveBufferDown());
                    break;
                case { Key: ConsoleKey.DownArrow, Modifiers: ConsoleModifiers.Control }:
                    OutControl(cb => cb.MoveBufferUp());
                    break;

                // control

                case { Char: '\\', Modifiers: 0 }:
                    _cursorConstrainMode = _cursorConstrainMode == CursorConstrainMode.Margin
                        ? CursorConstrainMode.Screen : CursorConstrainMode.Margin;
                    OutControl(cb =>
                    {
                        cb.SetCursorConstrainMode(_cursorConstrainMode);
                        cb.MoveCursorTo(_cursorPos.Y - _scrollTop, _cursorPos.X);
                    });
                    break;

                case { Char: 'l', Modifiers: ConsoleModifiers.Control }:
                    OutControl(cb =>
                    {
                        using var _ = cb.AutoSaveRestoreCursorState();
                        for (var line = _scrollTop; line <= _scrollBottom; ++line)
                        {
                            cb.MoveCursorTo(line, 0);
                            cb.ClearLine();
                        }
                    });
                    break;
                case { Char: 'l', Modifiers: ConsoleModifiers.Alt }:
                    OutControl(cb => cb.ClearScreen());
                    break;

                case { Char: '[', Modifiers: 0 }:
                    SetScrollMargin(_cursorPos.Y, _scrollBottom, out error);
                    break;
                case { Char: ']', Modifiers: 0 }:
                    SetScrollMargin(_scrollTop, _cursorPos.Y, out error);
                    break;

                case { Char: '[', Modifiers: ConsoleModifiers.Alt }:
                    ResetScrollMargin();
                    break;

                case { Key: ConsoleKey.UpArrow, Modifiers: ConsoleModifiers.Alt }:
                    SetScrollMargin(_scrollTop, _scrollBottom - 1, out error);
                    break;
                case { Key: ConsoleKey.DownArrow, Modifiers: ConsoleModifiers.Alt }:
                    SetScrollMargin(_scrollTop, _scrollBottom + 1, out error);
                    break;
                case { Key: ConsoleKey.LeftArrow, Modifiers: ConsoleModifiers.Alt }:
                    SetScrollMargin(_scrollTop - 1, _scrollBottom, out error);
                    break;
                case { Key: ConsoleKey.RightArrow, Modifiers: ConsoleModifiers.Alt }:
                    SetScrollMargin(_scrollTop + 1, _scrollBottom, out error);
                    break;

                case { Char: ' ', Modifiers: 0 }:
                    // do nothing
                    break;

                case { Char: 'c', Modifiers: ConsoleModifiers.Control }:
                    return UnixSignal.KeyboardInterrupt.AsCliExitCode();
                case { Char: 'q', Modifiers: 0 }:
                    return CliExitCode.Success;
            }

            PrintStatus(error);
        }

        return CliExitCode.Success;
    }

    void OutControl(Action<ControlBuilder> action)
    {
        _cb.Clear();
        action(_cb);
        Out(_cb.Span);
        _cb.Clear();
    }

    string StringControl(Action<ControlBuilder> action)
    {
        _cb.Clear();
        action(_cb);
        var s = _cb.ToString();
        _cb.Clear();
        return s;
    }

    void OutString(Action<StringBuilder> action)
    {
        _sb.Clear();
        action(_sb);
        Out(_sb);
        _sb.Clear();
    }

    static void Out(ReadOnlySpan<char> span) => Terminal.Out(span);
    static void Out(StringBuilder sb) => Terminal.Out(sb.ToString()); // FUTURE: cathode support iterating stringbuilder chunks
    static void OutLineRaw(string text = "") => Out($"{text}\r\n");

    void PrintStatus(string error)
    {
#       pragma warning disable RS0030
        {
            var (x, y) = Console.GetCursorPosition();
            _cursorPos = new(x, y);
        }
#       pragma warning restore RS0030

        using var _ = new SaveRestoreCursor(OutControl);

        OutControl(cb =>
        {
            // scroll margins
            if (_scrollTop != _scrollBottom)
            {
                cb  .MoveCursorTo(_scrollTop, 0)
                    .SetForegroundColor(Color.Yellow)
                    .Print("⎴")
                    .MoveCursorTo(_scrollBottom, 0)
                    .Print("⎵");
            }
            else
            {
                cb  .MoveCursorTo(_scrollTop, 0)
                    .SetForegroundColor(Color.Yellow)
                    .Print("⦗");
            }

            // status
            cb  .MoveCursorTo(10000, 1)
                .SetForegroundColor(Color.Cyan)
                .Print((
                    $"[ p={_cursorPos.X},{_cursorPos.Y} sz={_terminalSize.Width}:{_terminalSize.Height} "+
                    $"sc={_scrollTop}:{_scrollBottom} [{_cursorConstrainMode.ToString().ToLowerInvariant()}] "+
                    $"err={error} ]  "
                ).AsSpanSafe(0, _terminalSize.Width - 1));
        });
    }

    void SetScrollMargin(int top, int bottom, out string error)
    {
        using var _ = new SaveRestoreCursor(OutControl); // changing scroll marging always sets cursor to top left of region

        error = "";
        try
        {
            OutControl(cb => cb.SetScrollMargin(top, bottom));

            _scrollTop = top;
            _scrollBottom = bottom;
        }
        catch (ArgumentOutOfRangeException x)
        {
            error = $"out of range: {x.ParamName}";
        }
    }

    void ResetScrollMargin()
    {
        using var _ = new SaveRestoreCursor(OutControl); // changing scroll marging always sets cursor to top left of region
        OutControl(cb => cb.ResetScrollMargin());

        _scrollTop = 0;
        _scrollBottom = _terminalSize.Height-1;
    }

    static readonly string[] k_loremIpsum =
    [
        "accumsan", "accusam", "accusam", "ad", "adipiscing", "adipiscing", "aliquam", "aliquam", "aliquip", "aliquyam",
        "amet", "amet", "assum", "at", "at", "augue", "autem", "blandit", "clita", "clita", "commodo", "congue",
        "consectetuer", "consequat", "consetetur", "cum", "delenit", "delenit", "diam", "diam", "dignissim", "dolor",
        "dolore", "dolores", "dolores", "doming", "duis", "duis", "duo", "ea", "ea", "eirmod", "eirmod", "eleifend",
        "elit", "elitr", "enim", "eos", "erat", "eros", "esse", "est", "et", "et", "eu", "euismod", "eum", "ex",
        "exerci", "facer", "facilisi", "facilisis", "feugait", "feugiat", "gubergren", "hendrerit", "id", "illum",
        "imperdiet", "in", "invidunt", "ipsum", "iriure", "iusto", "justo", "kasd", "labore", "labore", "laoreet",
        "laoreet", "liber", "lobortis", "lorem", "luptatum", "luptatum", "magna", "mazim", "minim", "molestie",
        "molestie", "nam", "nibh", "nihil", "nisl", "no", "no", "nobis", "nonummy", "nonumy", "nostrud", "nulla", "odio",
        "option", "placerat", "possim", "praesent", "qui", "quis", "quis", "quod", "rebum", "sadipscing", "sanctus",
        "sea", "sea", "sed", "sed", "sit", "sit", "soluta", "stet", "suscipit", "takimata", "takimata", "tation",
        "tation", "te", "tempor", "tempor", "tincidunt", "ullamcorper", "ut", "ut", "vel", "velit", "veniam",
        "vero", "voluptua", "voluptua", "volutpat", "vulputate", "vulputate", "wisi", "zzril"
    ];

    static string NextLoremIpsum() => k_loremIpsum[Random.Shared.Next(k_loremIpsum.Length)];

    void PrintPatternLines(int count)
    {
        switch (_pattern)
        {
            case 0:
                _sb.Clear();
                var width = _terminalSize.Width - _cursorPos.X;

                for (var y = 0; y < count; ++y)
                {
                    while (_sb.Length < width)
                        _sb.Append(NextLoremIpsum() + " ");
                    var str = _sb.ToString();
                    _sb.Clear();

                    OutControl(cb =>
                    {
                        cb.Print(str.AsSpan(0, width));

                        if (y != count-1)
                        {
                            cb.LineFeed();
                            if (_cursorPos.Y != _scrollBottom)
                                ++_cursorPos.Y;
                        }

                        cb.MoveCursorTo(_cursorPos.Y, _cursorPos.X);
                    });
                }

                OutControl(cb => cb.MoveCursorTo(_cursorPos.Y, _cursorPos.X));
                break;

            default:
                throw new InvalidOperationException();
        }
    }

    void PrintPatternWords(int count)
    {
        switch (_pattern)
        {
            case 0:
                OutString(sb =>
                {
                    for (var i = 0; i < count; ++i)
                        sb.Append(NextLoremIpsum() + " ");
                });
                break;

            default:
                throw new InvalidOperationException();
        }
    }

    const string k_randomChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()-=_+[]{};':\",.<>/?`~";

    static char NextRandomChar() => k_randomChars[Random.Shared.Next(k_randomChars.Length)];

    void PrintRandomChars(int count) => OutString(sb =>
    {
        for (var i = 0; i < count; ++i)
            sb.Append(NextRandomChar());
    });

    const string k_driveHelp = """
        [u]fills[/]:
          f              fill screen with a pattern (repeat to cycle patterns)
          F1-F12         print this many lines with existing fill pattern
          1-9            print this many words with existing fill pattern
          !1-9           print this many random non-whitespace chars
          h ?            print this help again
        [u]moves[/]:
          ←↓↑→ home end  move cursor around/start line/end line
          +↓↑            down/up ignoring margin (no constraint, no scroll)
          ^home ^end     move cursor top-left/bottom-right screen
          enter          \r\n
          ^↓↑            scroll buffer up/down
        [u]control[/]:
          ^l !l          clear margin area / screen
          !←↓↑→          set scroll margin (↓↑ bottom, ←→ top)
          [ ] ![         set top / bottom scroll margin / clear it
          \              set cursor constrain mode screen / margin
          space          do nothing, run loop (re-print status)
          ^c q           quit
        """;

    void Help()
    {
        string ProcessHelp(string underline, string reset) => k_driveHelp
            .Replace("[u]", underline)
            .Replace("[/]", reset);

        string[] SplitHelp(string text) => text
            .Split('\n')
            .Select(l => l.TrimEnd())
            .ToArray();

        var markup = SplitHelp(
            ProcessHelp(
                    StringControl(cb => cb.SetDecorations(underline: true)),
                    StringControl(cb => cb.ResetAttributes()))
                .RegexReplace("[+!^]+", m => StringControl(cb => cb
                    .SetForegroundColor(Color.Magenta)
                    .Print(m.Value)
                    .ResetAttributes())));

        var raw = SplitHelp(ProcessHelp("", ""));
        var maxLen = raw.Max(l => l.Length);

        OutString(sb =>
        {
            void AppendPadLine(string markupLine = "", string rawLine = "")
            {
                sb.Append(markupLine);
                sb.Append(' ', maxLen+1 - rawLine.Length);
                sb.Append("\r\n");
            }

            AppendPadLine();
            for (var i = 0; i < markup.Length; ++i)
                AppendPadLine(markup[i], raw[i]);
            AppendPadLine();
        });
    }
}
