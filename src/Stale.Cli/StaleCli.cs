
const string programVersion = "0.1";

var verbose = true;

try
{
    if (args.FirstOrDefault() == "--verbose")
    {
        Terminal.OutLine("Enabling verbose mode");
        args = args.Skip(1).ToArray();
    }
    else
        verbose = false;

    var (exitCode, opts) = StaleCliArguments.CreateParser().Parse(
        args, programVersion, StaleCliArguments.Help, StaleCliArguments.Usage,
        outWriter: Terminal.TerminalOut.ToTextWriter(),
        errWriter: Terminal.StandardError.ToTextWriter(),
        wrapWidth: Terminal.Size.Width);
    if (exitCode != null)
        return (int)exitCode.Value;

    if (verbose)
        opts.DumpTerminal("opts");

    if (opts.ArgCommand != null)
    {
        Terminal.OutLine($"Command: `{CliUtility.CommandLineArgsToString(opts.ArgArg.Prepend(opts.ArgCommand))}`");
        if (opts.OptRecord != null)
            Terminal.OutLine($"  -> record to '{opts.OptRecord}'");
        return (int)CliExitCode.Success;
    }

    if (opts.OptPlay != null)
    {
        Terminal.OutLine($"Play: {opts.OptPlay}");
        if (opts.OptSpeed != null)
        {
            if (opts.OptSpeed.EndsWith('x'))
                Terminal.OutLine($"  -> at {opts.OptSpeed} speed");
            else
                Terminal.OutLine($"  -> at {opts.OptSpeed} msec per line");
        }
        return (int)CliExitCode.Success;
    }

    throw new UnreachableCodeException();
}
catch (Exception x)
{
    x.TerminalOut(verbose);
    return (int)CliExitCode.ErrorSoftware;
}
