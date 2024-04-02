using System.Diagnostics;
using System.Threading.Channels;
using Vezel.Cathode;
using Vezel.Cathode.Processes;

const string programVersion = "0.1";

var ctx = new Context();

Terminal.Signaled += signalContext =>
{
    if (signalContext.Signal == TerminalSignal.Interrupt)
        ctx.Cancel.Cancel();
};

try
{
    {
        ctx.IsVerbose = args.FirstOrDefault() == "--verbose";
        if (ctx.IsVerbose)
        {
            ctx.VerboseLine("Enabling verbose mode");
            args = args.Skip(1).ToArray();
        }

        var (exitCode, options) = StaleCliArguments.CreateParser().Parse(
            args, programVersion, StaleCliArguments.Help, StaleCliArguments.Usage,
            outWriter: Terminal.StandardOut.TextWriter,
            errWriter: Terminal.StandardError.TextWriter,
            wrapWidth: Terminal.Size.Width);
        if (exitCode != null)
            return (int)exitCode.Value;

        ctx.Options = options;
    }

    ctx.VerboseDump(ctx.Options);

    if (ctx.Options.ArgCommand != null)
    {
        // TODO: consider checking if it's a console app
        // (see IsWindowsApplication at PowerShell\src\System.Management.Automation\engine\NativeCommandProcessor.cs:1199)

        if (ctx.IsVerbose)
        {
            var msg = $"Command: `{CliUtility.CommandLineArgsToString(ctx.Options.ArgArg.Prepend(ctx.Options.ArgCommand))}`";
            if (ctx.Options.OptRecord != null)
                msg += $" (record to '{ctx.Options.OptRecord}')";
            ctx.VerboseLine(msg);
        }

        var cmd = new ChildProcessBuilder()
            .WithFileName(ctx.Options.ArgCommand)
            .WithArguments(ctx.Options.ArgArg)
            .WithRedirections(false, true, true)
            .WithCreateWindow(false)
            .WithWindowStyle(ProcessWindowStyle.Hidden)
            .WithCancellationToken(ctx.Cancel.Token)
            .WithThrowOnError(false)
            .Run();

        var captures = Channel.CreateUnbounded<Capture>(new UnboundedChannelOptions { SingleReader = true });

        var open = 0;

        async void Write(TextReader reader, bool isStdErr)
        {
            Terminal.OutLine($"> Adding reader for {(isStdErr ? "stderr" : "stdout")}");

            ++open;
            while (!ctx.Cancel.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ctx.Cancel.Token);
                if (line == null)
                {
                    if (--open == 0)
                        captures.Writer.Complete();
                    break;
                }

                await captures.Writer.WriteAsync(new Capture(isStdErr, DateTime.Now, line));
            }
        }

        _ = Task.Run(() => Write(cmd.StandardOut.TextReader, false), ctx.Cancel.Token);
        _ = Task.Run(() => Write(cmd.StandardError.TextReader, true), ctx.Cancel.Token);

        await foreach (var capture in captures.Reader.ReadAllAsync())
            Terminal.OutLine($"{DateTime.Now:HH:mm:ss.fff} -> {capture.When:HH:mm:ss.fff} >> {(capture.IsStdErr ? '!' : ' ')}{capture.Line}");

        Terminal.OutLine($"{ctx.Options.ArgCommand} EXITING with code {cmd.Completion.Result}");

        return (int)CliExitCode.Success;
    }

    if (ctx.Options.OptPlay != null)
    {
        Terminal.OutLine($"Play: {ctx.Options.OptPlay}");
        if (ctx.Options.OptSpeed != null)
        {
            if (ctx.Options.OptSpeed.EndsWith('x'))
                Terminal.OutLine($"  -> at {ctx.Options.OptSpeed} speed");
            else
                Terminal.OutLine($"  -> at {ctx.Options.OptSpeed} msec per line");
        }
        return (int)CliExitCode.Success;
    }

    throw new UnreachableCodeException();
}
catch (Exception x)
{
    ctx.Error(x);
    return (int)CliExitCode.ErrorSoftware;
}

readonly record struct Capture(bool IsStdErr, DateTime When, string Line);
