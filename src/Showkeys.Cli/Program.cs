using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text;
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

async Task<CliExitCode> Drive(bool save)
{
    EnableRawMode();
    using var _ = save ? new AlternateScreen(OutControl) : (IDisposable?)null;

    var size = Terminal.Size;
    OutLineRaw($"Terminal size: {size.Width}x{size.Height}");
    OutLineRaw();

    var driveHelp = """
        [u]fills[/]:
          f              fill screen with a pattern (repeat to cycle patterns)
          1-9            print this many lines with existing fill pattern
          ^1-9           print this many words with existing fill pattern
          h ?            print this help again

        [u]moves[/]:
          ←↓↑→ home end  move cursor around/start line/end line
          ^home ^end     move cursor top-left/bottom-right screen
          enter          \r\n
          ^↓↑            scroll buffer up/down

        [u]control[/]:
          ^l             clear screen
          !←↓↑→          set scroll margin (↓↑ bottom, ←→ top)
          space          do nothing, run loop (re-print status)
          ^c q           quit
        """
        .Replace("[u]", StringControl(c => c.SetDecorations(underline: true)))
        .Replace("[/]", StringControl(c => c.ResetAttributes()))
        .RegexReplace("[+!^]+", m => StringControl(c => c
            .SetForegroundColor(Color.Yellow)
            .Print(m.Value)
            .ResetAttributes()));

    void OutLineRaw(string text = "") => Out($"{text}\r\n");

    void Help()
    {
        Out(driveHelp.Split('\n').Select(l => l.TrimEnd()).StringJoin("\r\n"));
        OutLineRaw();
        OutLineRaw();
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

    var (scrollTop, scrollBottom) = (0, size.Height-1);

    void PrintStatus(string error)
    {
#       pragma warning disable RS0030
        var pos = Console.GetCursorPosition();
#       pragma warning restore RS0030

        using var _ = new SaveRestoreCursor(OutControl);

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
            .Print($"[ pos={pos.Left},{pos.Top} scroll={scrollTop}:{scrollBottom} error={error} ]".AsSpanSafe(0, size.Width-1)));
    }

    await foreach (var item in AnsiInput.SelectReadKeysAsync(TerminalIn))
    {
        var error = "";

        void SetScrollMargin(int top, int bottom)
        {
            if (scrollTop < 0)
            {
                error = "scrollTop < 0";
                return;
            }
            if (scrollBottom < scrollTop)
            {
                error = "scrollBottom < scrollTop";
                return;
            }

            using var _ = new SaveRestoreCursor(OutControl);
            OutControl(c => c.SetScrollMargin(top, bottom));
            scrollTop = top;
            scrollBottom = bottom;
        }

        switch (item)
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

            case { Char: var c and >= '1' and <= '9', Modifiers: ConsoleModifiers.Alt }:
                PrintPatternWords(c - '0');
                break;

            case { Char: 'h' or '?', Modifiers: 0 }:
                OutControl(c => c.MoveCursorTo(10000, 0));
                OutLineRaw();
                OutLineRaw();
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

            case { Key: ConsoleKey.UpArrow, Modifiers: ConsoleModifiers.Alt }:
                SetScrollMargin(scrollTop, scrollBottom-1);
                break;
            case { Key: ConsoleKey.DownArrow, Modifiers: ConsoleModifiers.Alt }:
                SetScrollMargin(scrollTop, scrollBottom+1);
                break;
            case { Key: ConsoleKey.LeftArrow, Modifiers: ConsoleModifiers.Alt }:
                SetScrollMargin(scrollTop-1, scrollBottom);
                break;
            case { Key: ConsoleKey.RightArrow, Modifiers: ConsoleModifiers.Alt }:
                SetScrollMargin(scrollTop+1, scrollBottom);
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
