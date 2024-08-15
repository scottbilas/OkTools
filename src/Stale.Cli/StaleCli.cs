using System.Diagnostics;
using System.Globalization;
using DocoptNet;
using Spectre.Console;
using Vezel.Cathode;

const string programVersion = "0.1";

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

using var ctx = new Context();
ctx[PerfStatType.Init].Start();
var logStats = false;

// ReSharper disable AccessToDisposedClosure
// ^ talking about ctx here, it's ok, it outlives everything

/* // doesn't do anything in raw mode; unsure we need it
Terminal.Signaled += signalContext =>
{
    if (signalContext.Signal != TerminalSignal.Interrupt)
        return;

    // TODO: pass through ctrl-c and attempt to let the child process exit gracefully
    // (may also need to handle ctrl-break, ctrl-close, etc)
    // (may also need to pass y/n confirmation thingy)

    ctx.Cancel();
};
*/

try
{
    if (!Terminal.StandardIn.IsInteractive)
        throw new CliErrorException(CliExitCode.ErrorUsage, "This app requires an interactive terminal");

    {
        var isVerbose = false;
        NPath? useCwd = null;

        // TODO: fix up this pre-config stuff, it's brittle, inconsistent error reporting (doesn't benefit from docopt parser checker)
        for (var i = 0; i < args.Length; ++i)
        {
            if (args[i] == "--verbose")
                isVerbose = true;
            else if (args[i] == "--stats")
                logStats = true;
            else if (args[i] == "--cwd")
            {
                if (++i == args.Length)
                    throw new DocoptInputErrorException("--cwd needs a path");
                useCwd = args[i];
            }
            else if (args[i] == "--dbgpause")
            {
                ctx.Out("Waiting for debugger to attach...");
                while (!Debugger.IsAttached)
                    Thread.Sleep(100);
                ctx.OutLine("attached!");
            }
            else
            {
                args = args[i..];
                break;
            }
        }

        if (isVerbose)
        {
            ctx.IsVerbose = true;
            ctx.VerboseLine("Enabling verbose mode");
        }

        if (logStats && ctx.IsVerbose)
            ctx.VerboseLine("Enabling stats printout at end");

        if (useCwd != null)
        {
            if (ctx.IsVerbose)
                ctx.VerboseLine($"Setting cwd to: {useCwd}");
            if (!useCwd.DirectoryExists())
                throw new DocoptInputErrorException($"--cwd path does not exist: {useCwd}");
            NPath.SetCurrentDirectory(useCwd);
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

    if (ctx.IsVerbose)
        ctx.VerboseDump(ctx.Options);

    if (ctx.Options.CmdRecord)
        return await Exec(async () => (int)await Playback.Record(ctx, ctx.Options.ArgRecorded, ctx.Options.ArgCommand!, [..ctx.Options.ArgArg]));

    if (ctx.Options is { CmdPlay: true, OptPassthru: true })
    {
        return await Exec(async () =>
        {
            var child = Playback.StartPlayback(ctx, new PlaybackOptions(ctx.Options));
            await foreach (var capture in child.Captures.ReadAllAsync(ctx.CancelToken))
                await (capture.IsStdErr ? Terminal.StandardError : Terminal.StandardOut).WriteLineAsync(capture.Line, ctx.CancelToken);
            return await child.Exited;
        });
    }

    var staleOptions = new StaleOptions(ctx.Options);

    ChildProcess child;
    if (ctx.Options.CmdPlay)
        child = Playback.StartPlayback(ctx, new PlaybackOptions(ctx.Options));
    else
    {
        var command = ctx.Options.ArgCommand!;
        IReadOnlyList<string> childArgs = [..ctx.Options.ArgArg];
        child = ChildProcess.ShellExec(ctx, command, childArgs);
    }

    return await Exec(async () => (int)await StaleApp.Run(ctx, staleOptions, child));
}
catch (CliErrorException x)
{
    ctx.ErrorLine(x.Message);
    ctx.LongTasks.AbortAll();
    return (int)x.Code;
}
catch (Exception x)
{
    ctx.Error(x);
    ctx.LongTasks.AbortAll();
    return (int)CliExitCode.ErrorSoftware;
}

void LogStats()
{
    ctx.OutMarkupLine("[aqua]-- Stats from run --[/]");
    var table = new Table() { Border = TableBorder.Minimal };
    table.AddColumn("Stat");
    table.AddColumn("Start");
    table.AddColumn("Stop");
    table.AddColumn("Elapsed");

    foreach (var statType in EnumUtility.GetValues<PerfStatType>())
    {
        var stat = ctx[statType];
        table.AddRow(
            statType.ToString(),
            stat.StartTime.ToString("hh:mm:ss.fff"),
            stat.StopTime.ToString("hh:mm:ss.fff"),
            stat.Elapsed.TotalSeconds.ToString("F3"));
    }

    var totalStart = ctx[EnumUtility.GetValues<PerfStatType>().First()].StartTime;
    var totalStop = ctx[EnumUtility.GetValues<PerfStatType>().Last()].StopTime;
    table.AddRow(
        "Total",
        totalStart.ToString("hh:mm:ss.fff"),
        totalStop.ToString("hh:mm:ss.fff"),
        (totalStop - totalStart).TotalSeconds.ToString("F3"));

    ctx.Out(table);
}

async Task<int> Exec(Func<Task<int>> task)
{
    ctx[PerfStatType.Init].Stop();

    ctx[PerfStatType.Command].Start();
    var result = await task();
    ctx[PerfStatType.Command].Stop();

    if (logStats)
        LogStats();

    return result;
}
