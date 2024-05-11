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

var cb = new ControlBuilder();

void Flush()
{
    Out(cb.Span);
    cb.Clear();
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
                ? await ShowKeysParser()
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
        cb.Clear();
        cb.SoftReset();
        Flush();

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

async Task<CliExitCode> ShowKeysParser()
{
    OutLine("Ctrl-C to quit");
    OutLine();

    EnableRawMode();

    await foreach (var item in AnsiInput.SelectReadKeysAsync(TerminalIn))
    {
        var (prefix, special, normal) = item.ToComponentStrings();
        cb.Clear();
        cb.SetForegroundColor(Color.Yellow);
        cb.Print(prefix);
        cb.SetForegroundColor(Color.Aqua);
        cb.Print(special);
        cb.ResetAttributes();
        cb.Print(normal);
        cb.Print("\r\n");
        await OutAsync(cb);

        if (item is { Char: 'c', Ctrl: true })
            return UnixSignal.KeyboardInterrupt.AsCliExitCode();
    }

    return CliExitCode.Success;
}

async Task<CliExitCode> Drive(bool save)
{
    EnableRawMode();
    using var _ = save ? new SaveScreen() : null;

    var size = Size;
    OutLineRaw($"Terminal size: {size.Width}x{size.Height}");
    OutLineRaw();

    const string driveHelp = """
        Keys:
         f     fill screen with a pattern (repeat to cycle patterns)
         h     print this help again
        ^c, q  quit
        """;

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

    await foreach (var item in AnsiInput.SelectReadKeysAsync(TerminalIn))
    {
        switch (item)
        {
            case { Char: 'f', Modifiers: 0 }:
                switch (pattern)
                {
                    case 0:
                        var sb = new StringBuilder();
                        for (var y = 0; y < size.Height; ++y)
                        {
                            while (sb.Length < size.Width)
                                sb.Append($"{loremIpsum[Random.Shared.Next(loremIpsum.Length)]} ");
                            Out(sb.ToString()[..size.Width]);
                            if (y != size.Height-1)
                                OutLineRaw();
                            sb.Clear();
                        }
                        Out('\r');
                        break;
                }

                if (++pattern == 1)
                    pattern = 0;
                break;

            case { Char: 'h', Modifiers: 0 }:
                Out(new ControlBuilder().MoveCursorTo(10000, 0));
                OutLineRaw();
                OutLineRaw();
                Help();
                break;

            case { Key: ConsoleKey.LeftArrow, Modifiers: 0 }:
                Out(new ControlBuilder().MoveCursorLeft(1));
                break;
            case { Key: ConsoleKey.RightArrow, Modifiers: 0 }:
                Out(new ControlBuilder().MoveCursorRight(1));
                break;
            case { Key: ConsoleKey.UpArrow, Modifiers: 0 }:
                Out(new ControlBuilder().MoveCursorUp(1));
                break;
            case { Key: ConsoleKey.DownArrow, Modifiers: 0 }:
                Out(new ControlBuilder().MoveCursorDown(1));
                break;

            case { Key: ConsoleKey.UpArrow, Modifiers: ConsoleModifiers.Control }:
                Out(new ControlBuilder().MoveBufferDown(1));
                break;
            case { Key: ConsoleKey.DownArrow, Modifiers: ConsoleModifiers.Control }:
                Out(new ControlBuilder().MoveBufferUp(1));
                break;

            case { Key: ConsoleKey.Enter, Modifiers: 0 }:
                Out("\r\n");
                break;

            case { Key: ConsoleKey.Home, Modifiers: 0 }:
                Out('\r');
                break;
            case { Key: ConsoleKey.Home, Modifiers: ConsoleModifiers.Control }:
                Out(new ControlBuilder().MoveCursorTo(0, 0));
                break;

            case { Key: ConsoleKey.End, Modifiers: 0 }:
                Out(new ControlBuilder().MoveCursorRight(10000));
                break;
            case { Key: ConsoleKey.End, Modifiers: ConsoleModifiers.Control }:
                Out(new ControlBuilder().MoveCursorTo(10000, 10000));
                break;

            case { Char: 'c', Modifiers: ConsoleModifiers.Control } or { Char: 'q', Modifiers: 0 }:
                return UnixSignal.KeyboardInterrupt.AsCliExitCode();
        }
    }

    return CliExitCode.Success;
}

class SaveScreen : IDisposable
{
    public SaveScreen()
    {
        Out("\x1b[?1049h");
    }

    public void Dispose()
    {
        Out("\x1b[?1049l");
    }
}
