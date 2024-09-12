using System.Diagnostics;
using System.Threading.Channels;
using Vezel.Cathode.Processes;

record ChildProcess(
    string Command,
    IReadOnlyList<string> Args,
    ChannelReader<LineCapture> Captures,
    int Id,
    Task<int> Exited)
{
    public static ChildProcess ShellExec(Context ctx, NPath command, IReadOnlyList<string> args)
    {
        var captures = Channel.CreateUnbounded<LineCapture>(new UnboundedChannelOptions { SingleReader = true });

        if (ShellExecUtility.ConfigureProcessExitToAlsoKillChildProcesses() && ctx.IsVerbose)
            ctx.VerboseLine("Configuring OS to kill child processes if the current process exits");

        // TODO: consider checking if it's a console app
        // (see IsWindowsApplication at PowerShell\src\System.Management.Automation\engine\NativeCommandProcessor.cs:1199)

        var (commandPath, extraArgs) = ShellExecUtility.ResolveShellCommand(command);
        if (extraArgs.Any())
            args = [..extraArgs, ..args];

        var process = new ChildProcessBuilder()
            .WithFileName(commandPath)
            .WithArguments(args)
            .WithRedirections(true, true, true)
            .WithCreateWindow(false)
            .WithWindowStyle(ProcessWindowStyle.Hidden)
            .WithCancellationToken(ctx.CancelToken)
            .WithThrowOnError(false)
            .Run();

        var open = 0;

        async Task CaptureLines(TextReader reader, bool isStdErr, CancellationToken stop)
        {
            Interlocked.Increment(ref open);

            while (!stop.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(stop);
                if (line == null)
                    break;

                await captures.Writer.WriteAsync(new LineCapture(isStdErr, DateTime.Now, line), stop);
            }

            if (Interlocked.Decrement(ref open) == 0)
            {
                // use the non-throwing TryComplete here to avoid a potential race in this function among the two
                // readers and the cancel token causing a double-complete.
                captures.Writer.TryComplete();
            }
        }

        ctx.LongTasks.Run($"StandardOut reader for pid {process.Id}", stop => CaptureLines(process.StandardOut.TextReader, false, stop));
        ctx.LongTasks.Run($"StandardError reader for pid {process.Id}", stop => CaptureLines(process.StandardError.TextReader, true, stop));

        return new(commandPath, args, captures.Reader, process.Id, process.Completion);
    }
}
