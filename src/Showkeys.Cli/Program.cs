using System.Runtime.CompilerServices;
using DocoptNet;
using OkTools.Core.Terminal;
using Vezel.Cathode.Text.Control;
using static Vezel.Cathode.Terminal;

const string help = """
    showkeys, the terminal utils thingy

    Usage:
      showkeys show [--dotnet | --ansi]
      showkeys drive
      showkeys --help

    Commands:
      show   Print out the ansi keycodes of the keys you press.
      drive  Exercise the terminal to see what different vt100 sequences do.

    Options:
      --dotnet  Use the built-in dotnet Console key reader in cooked mode, rather than Cathode (ansi sequences, raw mode).
      --ansi    Use OkTools.Core.Terminal.AnsiInputReaderReader to receive key events.
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
    OutLine("Ctrl-C to quit");
    OutLine();

    try
    {
        if (options["show"].IsTrue)
        {
            if (options["--dotnet"].IsTrue)
                return ShowKeysDotnet();

            EnableRawMode();

            return options["--ansi"].IsTrue
                ? await ShowKeysParserRaw()
                : ShowKeysCathodeRaw();
        }

        if (options["drive"].IsTrue)
            return await Drive();

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
    Console.CancelKeyPress += (_, _) => Environment.Exit((int)UnixSignal.KeyboardInterrupt.AsCliExitCode());

    for (;;)
    {
        var keyInfo = Console.ReadKey(true);

        OutLine($"key={keyInfo.Key} char='{CharUtils.ToNiceString(keyInfo.KeyChar)}' mod={keyInfo.Modifiers}");
    }
}
#pragma warning restore RS0030

CliExitCode ShowKeysCathodeRaw()
{
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

async Task<CliExitCode> ShowKeysParserRaw()
{
    await foreach (var item in AnsiInput.SelectReadKeysAsync(TerminalIn))
    {
        await OutAsync($"{item}\r\n");
        if (item is { Char: 'c', Ctrl: true })
            return UnixSignal.KeyboardInterrupt.AsCliExitCode();
    }

    return CliExitCode.Success;
}

async Task<CliExitCode> Drive()
{
    OutLine("Ctrl-C to quit");
    OutLine();

    EnableRawMode();

    await Task.Delay(0);

    return CliExitCode.Success;
}
