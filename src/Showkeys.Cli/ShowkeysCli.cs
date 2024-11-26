using System.Drawing;
using System.Runtime.CompilerServices;
using DocoptNet;
using OkTools.Terminal;
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
      --ansi    Use OkTools.Terminal.AnsiInputReaderReader to receive key events.
      --save    Save the screen before entering drive mode and restore it afterwards.
    """;

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
            return await new DriveCommand().RunAsync(options["--save"].IsTrue);

        throw new InvalidOperationException("Invalid command");
    }
    catch (CliErrorException ex)
    {
        return ex.Code;
    }
    finally
    {
        Out(new ControlBuilder().SoftReset());
        DisableRawMode();
    }
}

#pragma warning disable RS0030 // bypass Console.* analyzers
CliExitCode ShowKeysDotnet()
{
    Console.WriteLine("Ctrl-C to quit");
    Console.WriteLine();

    Console.CancelKeyPress += (_, _) => Environment.Exit((int)UnixSignal.KeyboardInterrupt.AsCliExitCode());

    for (;;)
    {
        var keyInfo = Console.ReadKey(true);

        Console.WriteLine($"key={keyInfo.Key} char='{CharUtils.ToNiceString(keyInfo.KeyChar)}' mod={keyInfo.Modifiers}");
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

    var cb = new ControlBuilder();

    await foreach (var item in AnsiInput.SelectReadKeysAsync(TerminalIn))
    {
        var (prefix, special, normal) = item.ToComponentStrings();
        cb
            .SetForegroundColor(Color.Yellow)
            .Print(prefix)
            .SetForegroundColor(Color.Aqua)
            .Print(special)
            .ResetAttributes()
            .Print(normal)
            .Print("\r\n");
        Out(cb);
        cb.Clear();

        if (item is { Char: 'c', Ctrl: true })
            return UnixSignal.KeyboardInterrupt.AsCliExitCode();
    }

    return CliExitCode.Success;
}
