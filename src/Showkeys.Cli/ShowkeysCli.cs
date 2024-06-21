using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using DocoptNet;
using OkTools.Core.Terminal;
using Vezel.Cathode;
using Vezel.Cathode.Text.Control;
using static Vezel.Cathode.Terminal;

const string help = """
    showkeys, the terminal utils thingy

    Usage:
      showkeys show [--dotnet | --ansi]
      showkeys drive [--save]
      showkeys --help

    Commands:
      show   Print out the ansi keycodes of the keys you press.
      drive  Exercise the terminal to see what different vt100 sequences do.

    Options:
      --dotnet  Use the built-in dotnet Console key reader in cooked mode, rather than Cathode (ansi sequences, raw mode).
      --ansi    Use OkTools.Core.Terminal.AnsiInputReaderReader to receive key events.
      --save    Save the screen before entering drive mode and restore it afterwards.
    """;

var controlBuilder = new ControlBuilder(100);

void OutControl(Action<ControlBuilder> action)
{
    controlBuilder.Clear();
    action(controlBuilder);
    Out(controlBuilder.Span);
    controlBuilder.Clear();
}

string StringControl(Action<ControlBuilder> action)
{
    action(controlBuilder);
    var s = controlBuilder.ToString();
    controlBuilder.Clear();
    return s;
}

return Docopt.CreateParser(help).Parse(args) switch
{
    IArgumentsResult<IDictionary<string, ArgValue>> result
        => (int)await Run(result.Arguments),
    IHelpResult
        => ShowHelp(),
    IInputErrorResult error
        => ShowError(error.Usage),
    var unknown
        => throw new SwitchExpressionException(unknown)
};

int ShowHelp() { OutLine(help); return (int)CliExitCode.Success; }
int ShowError(string usage) { ErrorLine(usage); return (int)CliExitCode.ErrorUsage; }

async Task<CliExitCode> Run(IDictionary<string, ArgValue> options)
{
    try
    {
        if (options["show"].IsTrue)
        {
            if (options["--dotnet"].IsTrue)
                return ShowKeysDotnet();

            return options["--ansi"].IsTrue
                ? await ShowKeysAnsiInput()
                : ShowKeysCathode();
        }

        if (options["drive"].IsTrue)
        {
            EnableRawMode();
            return await Drive(options["--save"].IsTrue);
        }

        throw new InvalidOperationException("Invalid command");
    }
    catch (CliErrorException ex)
    {
        return ex.Code;
    }
    finally
    {
        OutControl(c => c.SoftReset());
        DisableRawMode();
    }
}

#pragma warning disable RS0030 // bypass Console.* analyzers
CliExitCode ShowKeysDotnet()
{
    OutLine("Ctrl-C to quit");
    OutLine();

    Console.CancelKeyPress += (_, _) => Environment.Exit((int)UnixSignal.KeyboardInterrupt.AsCliExitCode());

    for (;;)
    {
        var keyInfo = Console.ReadKey(true);

        OutLine($"key={keyInfo.Key} char='{CharUtils.ToNiceString(keyInfo.KeyChar)}' mod={keyInfo.Modifiers}");
    }
// ReSharper disable once FunctionNeverReturns
}
#pragma warning restore RS0030

CliExitCode ShowKeysCathode()
{
    OutLine("Ctrl-C to quit");
    OutLine();

    EnableRawMode();

    var buffer = new byte[100];
    for (;;)
    {
        var read = Read(buffer);
        for (var i = 0; i < read; ++i)
        {
            var c = (char)buffer[i];
            Out(CharUtils.ToNiceString(c));

            if (c == ControlConstants.ETX)
                return UnixSignal.KeyboardInterrupt.AsCliExitCode();
        }
        Out("\r\n");
    }
}

async Task<CliExitCode> ShowKeysAnsiInput()
{
    OutLine("Ctrl-C to quit");
    OutLine();

    EnableRawMode();

    await foreach (var item in AnsiInput.SelectReadKeysAsync(TerminalIn))
    {
        var (prefix, special, normal) = item.ToComponentStrings();
        OutControl(c => c
            .SetForegroundColor(Color.Yellow)
            .Print(prefix)
            .SetForegroundColor(Color.Aqua)
            .Print(special)
            .ResetAttributes()
            .Print(normal)
            .Print("\r\n"));

        if (item is { Char: 'c', Ctrl: true })
            return UnixSignal.KeyboardInterrupt.AsCliExitCode();
    }

    return CliExitCode.Success;
}

// ReSharper disable AccessToModifiedClosure
async Task<CliExitCode> Drive(bool save)
{
    EnableRawMode();
    using var _ = save ? new AlternateScreen(OutControl) : (IDisposable?)null;

    var size = Terminal.Size;
    OutLineRaw($"Terminal size: {size.Width}x{size.Height}");

    var (scrollTop, scrollBottom) = (0, size.Height-1);

    var driveHelp = """
        [u]fills[/]:
          f              fill screen with a pattern (repeat to cycle patterns)
          1-9            print this many lines with existing fill pattern
          ^1-9           print this many words with existing fill pattern
          !1-9           print this many random non-whitespace chars
          h ?            print this help again

        [u]moves[/]:
          ←↓↑→ home end  move cursor around/start line/end line
          ^home ^end     move cursor top-left/bottom-right screen
          enter          \r\n
          ^↓↑            scroll buffer up/down

        [u]control[/]:
          ^l             clear screen
          !←↓↑→          set scroll margin (↓↑ bottom, ←→ top)
          [ ] ![         set top / bottom scroll margin / clear it
          space          do nothing, run loop (re-print status)
          ^c q           quit
        """;

    void OutLineRaw(string text = "") => Out($"{text}\r\n");

    void Help()
    {
        string ProcessHelp(string underline, string reset) => driveHelp
            .Replace("[u]", underline)
            .Replace("[/]", reset);

        string[] SplitHelp(string text) => text
            .Split('\n')
            .Select(l => l.TrimEnd())
            .ToArray();

        var markup = SplitHelp(
            ProcessHelp(
                StringControl(c => c.SetDecorations(underline: true)),
                StringControl(c => c.ResetAttributes()))
            .RegexReplace("[+!^]+", m => StringControl(c => c
                .SetForegroundColor(Color.Yellow)
                .Print(m.Value)
                .ResetAttributes())));

        var raw = SplitHelp(ProcessHelp("", ""));
        var maxLen = raw.Max(l => l.Length);

        var sb = new StringBuilder();

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

        Out(sb);
    }

    Help();

    var pattern = 0;

    // ReSharper disable StringLiteralTypo
    var loremIpsum = new[]
    {
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
    };
    // ReSharper restore StringLiteralTypo

    string NextLoremIpsum() => loremIpsum[Random.Shared.Next(loremIpsum.Length)];

    void PrintPatternLines(int count)
    {
        switch (pattern)
        {
            case 0:
                var sb = new StringBuilder();
                for (var y = 0; y < count; ++y)
                {
                    while (sb.Length < size.Width)
                        sb.Append(NextLoremIpsum() + " ");
                    Out(sb.ToString()[..size.Width]);
                    if (y != size.Height-1)
                        OutLineRaw();
                    sb.Clear();
                }
                Out('\r');
                break;

            default:
                throw new InvalidOperationException();
        }
    }

    void PrintPatternWords(int count)
    {
        switch (pattern)
        {
            case 0:
                var sb = new StringBuilder();
                for (var i = 0; i < count; ++i)
                    sb.Append(NextLoremIpsum() + " ");
                Out(sb.ToString());
                break;

            default:
                throw new InvalidOperationException();
        }
    }

    const string randomChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()-=_+[]{};':\",.<>/?`~";

    char NextRandomChar() => randomChars[Random.Shared.Next(randomChars.Length)];

    void PrintRandomChars(int count)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < count; ++i)
            sb.Append(NextRandomChar());
        Out(sb.ToString());
    }

    var cursorPos = (x: 0, y: 0);

    void PrintStatus(string error)
    {
#       pragma warning disable RS0030
        cursorPos = Console.GetCursorPosition();
#       pragma warning restore RS0030

        using var __ = new SaveRestoreCursor(OutControl);

        OutControl(c => c
            // scroll margins
            .MoveCursorTo(scrollTop, 0)
            .SetForegroundColor(Color.Yellow)
            .Print("⎴")
            .MoveCursorTo(scrollBottom, 0)
            .Print("⎵")
            // status
            .MoveCursorTo(10000, 1)
            .SetForegroundColor(Color.Cyan)
            .Print($"[ p={cursorPos.x},{cursorPos.y} sz={size.Width}:{size.Height} sc={scrollTop}:{scrollBottom} err={error} ]  ".AsSpanSafe(0, size.Width - 1)));
    }

    PrintStatus("");

    void SetScrollMargin(int top, int bottom, out string error)
    {
        if (top < 0)
        {
            error = "top < 0";
            return;
        }
        if (bottom <= top)
        {
            error = "bottom <= top";
            return;
        }
        if (bottom >= size.Height)
        {
            error = "bottom >= size.Height";
            return;
        }

        using var __ = new SaveRestoreCursor(OutControl); // changing scroll marging always sets cursor to top left of region
        OutControl(c => c.SetScrollMargin(top, bottom));

        scrollTop = top;
        scrollBottom = bottom;

        error = "";
    }

    var events = Channel.CreateUnbounded<IEvent>();

    // fire & forget, because ReadKeysAsync routes them into the channel and the `await foreach` will pick that up
    var ___ = Task.Run(() => AnsiInput.ReadKeysAsync(TerminalIn, events.Writer, e => new InputEvent(e)));

    Resized += newSize => events.Writer.TryWrite(new TerminalSizeEvent(newSize));

    await foreach (var evt in events.Reader.ReadAllAsync())
    {
        if (evt is TerminalSizeEvent sizeEvent)
        {
            // keep scroll bottom at end if we had that previously, otherwise clamp to new max
            var newBottom = scrollBottom;
            if (scrollBottom == size.Height-1 || scrollBottom >= sizeEvent.NewSize.Height)
                newBottom = sizeEvent.NewSize.Height-1;

            // maintain scroll margin height if we can
            var newTop = scrollTop;
            if (scrollTop >= newBottom)
                newTop = Math.Max(0, newBottom - (scrollBottom - scrollTop));

            // now we can commit the size (do this before SetScrollMargin)
            size = sizeEvent.NewSize;

            SetScrollMargin(newTop, newBottom, out var _);
            OutLineRaw($"Terminal resized: {size.Width}x{size.Height}");
            PrintStatus("");

            continue;
        }

        var keyEvent = ((InputEvent)evt).KeyEvent;
        var error = "";

        void ResetScrollMargin()
        {
            using var __ = new SaveRestoreCursor(OutControl); // changing scroll marging always sets cursor to top left of region
            OutControl(c => c.Print(ControlConstants.CSI).Print(";r")); // consider adding to cathode

            scrollTop = 0;
            scrollBottom = size.Height-1;
        }

        switch (keyEvent)
        {
            // fills

            case { Char: 'f', Modifiers: 0 }:
                PrintPatternLines(size.Height);
                if (++pattern == 1)
                    pattern = 0;
                break;

            case { Char: var c and >= '1' and <= '9', Modifiers: 0 }:
                PrintPatternLines(c - '0');
                break;

            case { Char: var c and >= '1' and <= '9', Modifiers: ConsoleModifiers.Control }:
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
                OutControl(c => c.MoveCursorLeft(1));
                break;
            case { Key: ConsoleKey.RightArrow, Modifiers: 0 }:
                OutControl(c => c.MoveCursorRight(1));
                break;
            case { Key: ConsoleKey.UpArrow, Modifiers: 0 }:
                OutControl(c => c.MoveCursorUp(1));
                break;
            case { Key: ConsoleKey.DownArrow, Modifiers: 0 }:
                OutControl(c => c.MoveCursorDown(1));
                break;

            case { Key: ConsoleKey.Home, Modifiers: 0 }:
                Out('\r');
                break;
            case { Key: ConsoleKey.Home, Modifiers: ConsoleModifiers.Control }:
                OutControl(c => c.MoveCursorTo(0, 0));
                break;

            case { Key: ConsoleKey.End, Modifiers: 0 }:
                OutControl(c => c.MoveCursorRight(10000));
                break;
            case { Key: ConsoleKey.End, Modifiers: ConsoleModifiers.Control }:
                OutControl(c => c.MoveCursorTo(10000, 10000));
                break;

            case { Key: ConsoleKey.Enter, Modifiers: 0 }:
                Out("\r\n");
                break;

            case { Key: ConsoleKey.UpArrow, Modifiers: ConsoleModifiers.Control }:
                OutControl(c => c.MoveBufferDown(1));
                break;
            case { Key: ConsoleKey.DownArrow, Modifiers: ConsoleModifiers.Control }:
                OutControl(c => c.MoveBufferUp(1));
                break;

            // control

            case { Char: 'l', Modifiers: ConsoleModifiers.Control }:
                OutControl(c => c.ClearScreen());
                break;

            case { Char: '[', Modifiers: 0 }:
                SetScrollMargin(cursorPos.y, scrollBottom, out error);
                break;
            case { Char: ']', Modifiers: 0 }:
                SetScrollMargin(scrollTop, cursorPos.y, out error);
                break;

            case { Char: '[', Modifiers: ConsoleModifiers.Alt }:
                ResetScrollMargin();
                break;

            case { Key: ConsoleKey.UpArrow, Modifiers: ConsoleModifiers.Alt }:
                SetScrollMargin(scrollTop, scrollBottom - 1, out error);
                break;
            case { Key: ConsoleKey.DownArrow, Modifiers: ConsoleModifiers.Alt }:
                SetScrollMargin(scrollTop, scrollBottom + 1, out error);
                break;
            case { Key: ConsoleKey.LeftArrow, Modifiers: ConsoleModifiers.Alt }:
                SetScrollMargin(scrollTop - 1, scrollBottom, out error);
                break;
            case { Key: ConsoleKey.RightArrow, Modifiers: ConsoleModifiers.Alt }:
                SetScrollMargin(scrollTop + 1, scrollBottom, out error);
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
// ReSharper restore AccessToModifiedClosure

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
