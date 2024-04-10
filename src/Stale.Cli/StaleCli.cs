using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using DocoptNet;
using Vezel.Cathode;
using Vezel.Cathode.Processes;

const string programVersion = "0.1";
const int jsonVersion = 1;

using var ctx = new Context();

// ReSharper disable AccessToDisposedClosure
// ^ talking about ctx here, it's ok, it outlives everything

Terminal.Signaled += signalContext =>
{
    if (signalContext.Signal != TerminalSignal.Interrupt)
        return;

    ctx.Cancel();
};

try
{
    {
        var isVerbose = false;
        NPath? useCwd = null;

        // TODO: fix up this pre-config stuff, it's brittle, inconsistent error reporting (doesn't benefit from docopt parser checker)
        for (var i = 0; i < args.Length; ++i)
        {
            if (args[i] == "--verbose")
                isVerbose = true;
            else if (args[i] == "--cwd")
            {
                if (++i == args.Length)
                    throw new DocoptInputErrorException("--cwd needs a path");
                useCwd = args[i];
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
        return (int)await Record(ctx.Options.ArgRecorded, ctx.Options.ArgCommand!, ctx.Options.ArgArg.ToArray());

    if (ctx.Options.CmdPlay)
    {
        double? ratio = null;
        int? delay = null;

        if (ctx.Options.OptSpeed != null)
        {
            var success = false;

            var m = Regex.Match(ctx.Options.OptSpeed, @"(?<ratio>[0-9.])x|(?<delay>\d+)ms");
            var ratioGroup = m.Groups["ratio"];
            var delayGroup = m.Groups["delay"];

            if (ratioGroup.Success)
            {
                if (double.TryParse(ratioGroup.Value, out var value) && value > 0)
                {
                    ratio = value;
                    success = true;
                }
            }
            else if (delayGroup.Success)
            {
                if (int.TryParse(delayGroup.Value, out var value) && value >= 0)
                {
                    delay = value;
                    success = true;
                }
            }

            if (!success)
            {
                throw new CliErrorException(
                    CliExitCode.ErrorUsage,
                    "Unable to parse speed argument; needs to be a ratio like 1.23x or a delay like 456ms");
            }
        }

        if (ctx.IsVerbose)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (ratio != null && ratio != 1.0)
                ctx.VerboseLine($"Playing back '{ctx.Options.ArgRecorded}' at {ratio}x speed");
            else if (delay == 0)
                ctx.VerboseLine($"Playing back '{ctx.Options.ArgRecorded}' at maximum speed");
            else if (delay != null)
                ctx.VerboseLine($"Playing back '{ctx.Options.ArgRecorded}' at {delay}ms per line");
            else
                ctx.VerboseLine($"Playing back '{ctx.Options.ArgRecorded}' at original speed");
        }

        return (int)await Play(ctx.Options.ArgRecorded!, ratio, delay, ctx.CancelToken);
    }

    // $$$ DO THE REAL PROGRAM HERE

    throw new UnreachableCodeException();
}
catch (CliErrorException x)
{
    ctx.ErrorLine(x.Message);
    return (int)x.Code;
}
catch (Exception x)
{
    ctx.Error(x);
    return (int)CliExitCode.ErrorSoftware;
}

async Task<CliExitCode> Record(string? recordedPath, string command, IReadOnlyList<string> args)
{
    var (cmd, captures) = Exec(command, args);

    if (ctx.IsVerbose)
    {
        ctx.VerboseLine($">{cmd.Id} $ {command} {args}");
        ctx.VerboseLine($" (Recording to ${ctx.Options.ArgRecorded ?? "stdout"})");
    }

    var jsonStream = recordedPath != null
        ? File.Create(recordedPath)
        : Terminal.StandardOut.Stream;

    await using var writer = new StreamWriter(jsonStream);
    writer.AutoFlush = true;

    await using var json = new Utf8JsonWriter(jsonStream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    json.WriteStartObject();
    json.WriteNumber("version", jsonVersion);
    json.WriteStartArray("captures");
    json.Flush();
    writer.Write('\n');

    await foreach (var capture in captures.ReadAllAsync(ctx.CancelToken))
    {
        writer.Write('\n');

        json.WriteStartObject();
        if (capture.IsStdErr)
            json.WriteBoolean("isStdErr", true);
        json.WriteString("when", capture.When);
        json.WriteString("line", capture.Line);
        json.WriteEndObject();
        json.Flush();
    }

    json.WriteEndArray();
    json.Flush();
    writer.Write("\n\n");

    json.WriteNumber("exitcode", cmd.Completion.Result);

    json.WriteEndObject();
    json.Flush();

    writer.Write('\n');

    return CliExitCode.Success;
}

async Task<CliExitCode> Play(string recordedPath, double? ratio, int? delay, CancellationToken cancel)
{
    using var reader = new StreamReader(File.OpenRead(recordedPath));

    var json = await JsonDocument.ParseAsync(reader.BaseStream, default, cancel);

    var version = json.RootElement.GetProperty("version").GetInt32();
    if (version != jsonVersion)
        throw new CliErrorException(CliExitCode.ErrorDataErr, $"Recorded file '{recordedPath}' is version {{version}}, expected {{jsonVersion}}");

    var captures = json.RootElement.GetProperty("captures");
    var exitCode = json.RootElement.GetProperty("exitcode").GetInt32();

    if (ratio == null && delay == null)
        ratio = 1;

    DateTime? last = null;

    foreach (var capture in captures.EnumerateArray())
    {
        var isStdErr = capture.TryGetProperty("isStdErr", out var isStdErrEl) && isStdErrEl.GetBoolean();
        var when = capture.GetProperty("when").GetDateTime();
        var line = capture.GetProperty("line").GetString();

        if (ratio != null)
        {
            if (last != null)
            {
                var delta = (when - last.Value).TotalMilliseconds;
                await Task.Delay((int)(delta * ratio.Value), cancel); // returns CompletedTask if delay (int ms) is 0
            }
            last = when;
        }
        else if (delay != 0)
            await Task.Delay(delay!.Value, cancel);

        if (isStdErr)
            Terminal.StandardError.WriteLine(line);
        else
            Terminal.StandardOut.WriteLine(line);
    }

    return (CliExitCode)exitCode;
}

(ChildProcess cmd, ChannelReader<Capture> reader) Exec(NPath command, IReadOnlyList<string> args)
{
    // TODO: consider checking if it's a console app
    // (see IsWindowsApplication at PowerShell\src\System.Management.Automation\engine\NativeCommandProcessor.cs:1199)

    var (commandPath, extraArgs) = ShellExecUtility.ResolveShellCommand(command);
    if (extraArgs.Any())
        args = [..extraArgs, ..args];

    var cmd = new ChildProcessBuilder()
        .WithFileName(commandPath)
        .WithArguments(args)
        .WithRedirections(false, true, true)
        .WithCreateWindow(false)
        .WithWindowStyle(ProcessWindowStyle.Hidden)
        .WithCancellationToken(ctx.CancelToken)
        .WithThrowOnError(false)
        .Run();

    var captures = Channel.CreateUnbounded<Capture>(new UnboundedChannelOptions { SingleReader = true });

    var open = 0;

    async void Write(TextReader reader, bool isStdErr)
    {
        Interlocked.Increment(ref open);

        while (!ctx.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ctx.CancelToken);
            if (line == null)
            {
                if (Interlocked.Decrement(ref open) == 0)
                    captures.Writer.Complete();
                break;
            }

            await captures.Writer.WriteAsync(new Capture(isStdErr, DateTime.Now, line), ctx.CancelToken);
        }
    }

    _ = Task.Run(() => Write(cmd.StandardOut.TextReader, false), ctx.CancelToken);
    _ = Task.Run(() => Write(cmd.StandardError.TextReader, true), ctx.CancelToken);

    return (cmd, captures.Reader);
}

readonly record struct Capture(bool IsStdErr, DateTime When, string Line)
{
    public override string ToString() => $"{(IsStdErr ? "! " : "")}{Line.SimpleEscape()} ({When:g})";
}
