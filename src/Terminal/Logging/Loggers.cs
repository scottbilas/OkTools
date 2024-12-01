using System.Reflection;
using Spectre.Console;

namespace OkTools.Terminal;

// general note on newline: always use \n rather than Environment.NewLine, because \r\n
// sucks. library funcs often use Environment.NewLine so avoid their "WriteLine" calls too.

public interface ILogger
{
    void Write(string text);
    void WriteLine(string text = "");

    Task WriteAsync(string text);
    Task WriteLineAsync(string text = "");

    void WriteException(Exception x) =>
        WriteLine(x.ToString());

    string ObjectToString(CliContext ctx, object item, string name, Func<PropertyInfo, bool>? filter = null) =>
        item.ToDumpString(
            name: name,
            quiet: ctx.DebugLevel <= DebugLevel.Normal,
            filter: filter,
            wrapWidth: ctx.WrapWidth,
            maxWrapLines: ctx.DebugLevel > DebugLevel.Normal ? 0 : 5);

    void WriteObject(CliContext ctx, object item, string name, Func<PropertyInfo, bool>? filter = null) =>
        WriteLine(ObjectToString(ctx, item, name, filter));

    Task WriteObjectAsync(CliContext ctx, object item, string name, Func<PropertyInfo, bool>? filter = null) =>
        WriteLineAsync(ObjectToString(ctx, item, name, filter));
}

public interface ITrackedLogger : ILogger
{
    int LinesWritten { get; }
    bool Any => LinesWritten > 0;
}

public class TrackedLogger(ILogger logger) : ITrackedLogger
{
    public int LinesWritten { get; private set; }

    public void Write(string text)
    {
        LinesWritten += text.Count(c => c == '\n');
        logger.Write(text);
    }

    public void WriteLine(string text = "")
    {
        LinesWritten += text.Count(c => c == '\n') + 1;
        logger.WriteLine(text);
    }

    public Task WriteAsync(string text)
    {
        LinesWritten += text.Count(c => c == '\n');
        return logger.WriteAsync(text);
    }

    public Task WriteLineAsync(string text = "")
    {
        LinesWritten += text.Count(c => c == '\n') + 1;
        return logger.WriteLineAsync(text);
    }
}

public class TextWriterLogger : ILogger
{
    protected readonly TextWriter Writer;

    public TextWriterLogger(TextWriter writer) => Writer = writer;

    public void Write(string text) => Writer.Write(text);
    public Task WriteAsync(string text) => Writer.WriteAsync(text);

    // even though TextWriter's own internal impl does two writes (one for text, one for \n), we do it here as a single
    // string to reduce the chance multithreaded writes will interleave newlines. don't like the alloc but /shrug need
    // to upgrade the interface to support spans and begin/end synchronization or whatever etc.
    public void WriteLine(string text = "") => Write(text + '\n');
    public Task WriteLineAsync(string text = "") => WriteAsync(text + '\n');
}

public sealed class AnsiConsoleLogger(Color color) : ILogger
{
    public void Write(string text)
    {
        if (text == "")
            return;

        using var _ = new SkipSpectreWordWrap();

        AnsiConsole.Foreground = color;
        //AnsiConsole.Write(text);
        // bug workaround: https://github.com/spectreconsole/spectre.console/issues/1387
        AnsiConsole.Write("{0}", text);
        AnsiConsole.ResetColors();
    }

    public void WriteLine(string text)
    {
        using var _ = new SkipSpectreWordWrap();

        if (text == "")
        {
            AnsiConsole.Write("\n");
            return;
        }

        AnsiConsole.Foreground = color;
        //AnsiConsole.Write(text + "\n"); // string concat matches internal WriteLine impl
        // bug workaround: https://github.com/spectreconsole/spectre.console/issues/1387
        AnsiConsole.Write("{0}\n", text);
        AnsiConsole.ResetColors();
    }

    // AnsiConsole doesn't support async (yet?)
    public Task WriteAsync(string text)
        { Write(text); return Task.CompletedTask; }
    public Task WriteLineAsync(string text = "")
        { WriteLine(text); return Task.CompletedTask; }

    public void WriteException(Exception x) =>
        AnsiConsole.WriteException(x, ExceptionFormats.ShortenEverything | ExceptionFormats.ShowLinks);

    // TODO: nice formatted WriteObject (table etc. but single color to match ctor color)
}
