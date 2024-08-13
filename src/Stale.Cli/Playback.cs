using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Vezel.Cathode;
using Vezel.Cathode.Text.Control;

readonly record struct LineCapture(bool IsStdErr, DateTime When, string Line)
{
    public override string ToString() => $"{(IsStdErr ? "⨯ " : "")}{Line.SimpleEscape()} ({When:g})";
}

readonly struct PlaybackOptions
{
    public PlaybackOptions(StaleCliArguments args)
    {
        Path = args.ArgRecorded!;

        if (args.OptSpeed != null)
        {
            var success = false;

            var m = Regex.Match(args.OptSpeed, @"(?<ratio>[0-9.]+)x|(?<delay>\d+)ms");
            var (ratioGroup, delayGroup) = (m.Groups["ratio"], m.Groups["delay"]);

            if (ratioGroup.Success)
            {
                if (double.TryParse(ratioGroup.Value, out var value) && value > 0)
                    (Ratio, success) = (value, true);
            }
            else if (delayGroup.Success)
            {
                if (int.TryParse(delayGroup.Value, out var value) && value >= 0)
                    (Delay, success) = (value, true);
            }

            if (!success)
                throw new CliErrorException(CliExitCode.ErrorUsage, "Unable to parse speed argument; needs to be a ratio like 1.23x or a delay like 456ms");
        }
        else
            Ratio = 1;
    }

    public readonly string  Path;

    // null == unconfigured == play back at original speed
    public readonly double? Ratio;
    public readonly int?    Delay;
}

static class Playback
{
    const int k_jsonRecordingVersion = 2;

    public static async Task<CliExitCode> Record(Context ctx, string? recordedPath, string command, IReadOnlyList<string> args)
    {
        var child = TerminalUtils.ShellExec(ctx, command, args);

        if (ctx.IsVerbose)
        {
            ctx.VerboseLine($">{child.Id} $ {command} {CliUtility.CommandLineArgsToString(args)}");
            ctx.VerboseLine($" (Recording to {ctx.Options.ArgRecorded ?? "stdout"}) ");
        }

        var jsonStream = recordedPath != null
            ? File.Create(recordedPath)
            : Terminal.StandardOut.Stream;

        await using var writer = new StreamWriter(jsonStream);
        writer.AutoFlush = true;

        await using var json = new Utf8JsonWriter(jsonStream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        void JsonNewline()
        {
            json.Flush();
            writer.Write('\n');
        }

        json.WriteStartObject();

        json.WriteNumber("version", k_jsonRecordingVersion);
        JsonNewline();

        json.WriteString("command", command);
        JsonNewline();
        // ReSharper disable once MethodHasAsyncOverload
        writer.Write("    ");
        json.WriteStartArray("args");
        foreach (var arg in args)
            json.WriteStringValue(arg);
        json.WriteEndArray();
        JsonNewline();

        var captureCount = 0;
        var spinnerLast = 0;
        const string spinner = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏";
        DateTime statusLast = DateTime.Now;
        var cb = new ControlBuilder();

        void UpdateStatus(bool isStdErr)
        {
            // write a dot every hundred lines
            if (captureCount % 100 == 0)
                ctx.Out(".");

            // spinner every 100ms
            var now = DateTime.Now;
            if ((now-statusLast).TotalSeconds > 0.1)
            {
                statusLast = now;
                cb.SaveCursorState();
                if (isStdErr)
                    cb.Print('⨯');
                else
                {
                    cb.Print(spinner[spinnerLast++]);
                    spinnerLast %= spinner.Length;
                }
                cb.RestoreCursorState();
                ctx.Out(cb.Span);
                cb.Clear();
            }
        }

        DateTime? captureStart = null;
        await foreach (var capture in child.Captures.ReadAllAsync(ctx.CancelToken))
        {
            if (captureStart == null)
            {
                captureStart = capture.When;
                json.WriteString("start", captureStart.Value);
                JsonNewline();
                json.WriteStartArray("captures");
                JsonNewline();
            }

            JsonNewline();

            json.WriteStartObject();
            if (capture.IsStdErr)
                json.WriteBoolean("isStdErr", true);
            json.WriteString("offset", $"{capture.When-captureStart:g}");
            json.WriteString("line", capture.Line);
            json.WriteEndObject();

            ++captureCount;
            UpdateStatus(capture.IsStdErr);
        }

        if (captureStart != null)
            json.WriteEndArray();
        JsonNewline();
        JsonNewline();

        json.WriteNumber("exitcode", await child.Exited);

        json.WriteEndObject();
        JsonNewline();

        ctx.OutLine(" ");

        return CliExitCode.Success;
    }

    public static StaleChildProcess StartPlayback(Context ctx, PlaybackOptions playbackOptions)
    {
        var captures = Channel.CreateUnbounded<LineCapture>(new UnboundedChannelOptions { SingleReader = true });

        if (ctx.IsVerbose)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (playbackOptions.Ratio != 1.0)
                ctx.VerboseLine($"Playing back '{ctx.Options.ArgRecorded}' at {playbackOptions.Ratio}x speed");
            else if (playbackOptions.Delay == 0)
                ctx.VerboseLine($"Playing back '{ctx.Options.ArgRecorded}' at maximum speed");
            else if (playbackOptions.Delay != null)
                ctx.VerboseLine($"Playing back '{ctx.Options.ArgRecorded}' at {playbackOptions.Delay}ms per line");
            else
                ctx.VerboseLine($"Playing back '{ctx.Options.ArgRecorded}' at original speed");
        }

        using var reader = new StreamReader(File.OpenRead(playbackOptions.Path));

        if (ctx.IsVerbose)
            ctx.VerboseLine($"Deserializing json from '{playbackOptions.Path}'...");
        var records = JsonDocument.ParseAsync(reader.BaseStream, default, ctx.CancelToken).Result.RootElement;

        var version = records.GetProperty("version").GetInt32();
        if (version != k_jsonRecordingVersion)
            throw new CliErrorException(CliExitCode.ErrorDataErr, $"Recorded file '{playbackOptions.Path}' is version {{version}}, expected {{jsonVersion}}");

        var command  = records.GetProperty("command").GetString()!;
        var args     = records.GetProperty("args").EnumerateArray().Select(arg => arg.GetString()!).ToList();
        var start    = records.GetProperty("start").GetDateTime();
        var exitCode = records.GetProperty("exitcode").GetInt32();

        async Task PlayLines(CancellationToken stopToken)
        {
            DateTime? last = null;
            foreach (var capture in records.GetProperty("captures").EnumerateArray())
            {
                if (stopToken.IsCancellationRequested)
                    return;

                var isStdErr = capture.TryGetProperty("isStdErr", out var isStdErrEl) && isStdErrEl.GetBoolean();
                var when = start + TimeSpan.Parse(capture.GetProperty("offset").GetString()!);
                var line = capture.GetProperty("line").GetString()!;

                if (playbackOptions.Ratio != null)
                {
                    if (last != null)
                    {
                        var delta = (when - last.Value).TotalMilliseconds;
                        if (delta != 0)
                            await Task.Delay((int)(delta / playbackOptions.Ratio!.Value), stopToken);
                    }
                    last = when;
                }
                else if (playbackOptions.Delay != 0)
                    await Task.Delay(playbackOptions.Delay!.Value, stopToken);

                await captures.Writer.WriteAsync(new(isStdErr, when, line), stopToken);
            }

            captures.Writer.Complete();
        }

        // this is a standalone task rather than looping async, to keep it completely independent of stale processing
        var longTask = ctx.LongTasks.Run($"Playback of '{playbackOptions.Path}'", PlayLines);
        return new(command, args, captures.Reader, -1, longTask.Task.ContinueWith(_ => exitCode));
    }
}
