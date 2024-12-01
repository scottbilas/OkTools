using System.Text.Json;
using Spectre.Console;

namespace OkTools.Terminal;

public interface IStatusLogger : ILogger
{
    // TODO: would be nice to have DryRunLine break lines at the wrap margin and put a [dry-run] on each. (requires context access.)

    bool SupportsColor => false;
    void DryRunLine(string text);
    void Markup(string text);
    void MarkupLine(string text);

    Task DryRunLineAsync(string text);
    Task MarkupAsync(string text);
    Task MarkupLineAsync(string text);

    void WriteOrDryRunLine(bool dowit, string text)
    {
        if (dowit)
            WriteLine(text);
        else
            DryRunLine(text);
    }

    Task WriteOrDryRunLineAsync(bool dowit, string text) =>
        dowit ? WriteLineAsync(text) : DryRunLineAsync(text);
}

public sealed class AnsiStatusWriterLogger(TextWriter writer) : TextWriterLogger(writer), IStatusLogger
{
    public bool SupportsColor => true;

    public void DryRunLine(string text)
    {
        using var _ = new SkipSpectreWordWrap();
        AnsiConsole.Markup("[grey italic][[dry-run]][/] " + text.EscapeMarkup() + '\n'); // TODO: MarkupInterpolated?
    }

    public void Markup(string text)
    {
        using var _ = new SkipSpectreWordWrap();
        AnsiConsole.Markup(text);
    }

    public void MarkupLine(string text)
    {
        using var _ = new SkipSpectreWordWrap();
        AnsiConsole.Markup(text + '\n'); // string concat matches internal MarkupLine impl
    }

    // AnsiConsole doesn't support async (yet?)
    public Task DryRunLineAsync(string text)
        { DryRunLine(text); return Task.CompletedTask; }
    public Task MarkupAsync(string text)
        { Markup(text); return Task.CompletedTask; }
    public Task MarkupLineAsync(string text)
        { MarkupLine(text); return Task.CompletedTask; }
}

public sealed class StatusWriterLogger : TextWriterLogger, IStatusLogger
{
    public StatusWriterLogger(TextWriter writer) : base(writer) {}

    public void DryRunLine(string text) =>
        Writer.WriteLine($"[dry-run] {text}");
    public void Markup(string text) =>
        Writer.Write(text.RemoveMarkup());
    public void MarkupLine(string text) =>
        Writer.WriteLine(text.RemoveMarkup());

    public Task DryRunLineAsync(string text) =>
        Writer.WriteLineAsync($"[dry-run] {text}");
    public Task MarkupAsync(string text) =>
        Writer.WriteAsync(text.RemoveMarkup());
    public Task MarkupLineAsync(string text) =>
        Writer.WriteLineAsync(text.RemoveMarkup());
}

public sealed class JsonLogger(TextWriter writer, string stream) : IStatusLogger
{
    // TODO: this is copy pasta, switch it to nicer """ single template thing
    string Begin() =>
        $"{{\"timestamp\":\"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\", \"stream\":\"{stream}\",";
    string Middle(string text) =>
        $"\"text\":\"{JsonEncodedText.Encode(text)}\"";
    string End() =>
        "}\n";

    public void Write(string text)
    {
        writer.Write(Begin());
        writer.Write(Middle(text));
        writer.Write(End());
    }

    public void WriteLine(string text) =>
        Write(text + "\n");

    public async Task WriteAsync(string text)
    {
        await writer.WriteAsync(Begin());
        await writer.WriteAsync(Middle(text));
        await writer.WriteAsync(End());
    }

    public Task WriteLineAsync(string text) =>
        WriteAsync(text + "\n");

    // TODO: write exception as json object
    //public void WriteException(Exception x)
    // TODO: serialize object to json
    //public void WriteObject(CliContextCliApp ctx, object item, string name, Func<PropertyInfo, bool>? filter = null)

    // TODO: nicer to use a json property for dry-run-ness
    static string WrapDryRun(string text) => "[dry-run] " + text + '\n';

    public void DryRunLine(string text) =>
        Write(WrapDryRun(text));
    public void Markup(string text) =>
        Write(text.RemoveMarkup());
    public void MarkupLine(string text) =>
        WriteLine(text.RemoveMarkup());

    public Task DryRunLineAsync(string text) =>
        WriteAsync(WrapDryRun(text));
    public Task MarkupAsync(string text) =>
        WriteAsync(text.RemoveMarkup());
    public Task MarkupLineAsync(string text) =>
        WriteLineAsync(text.RemoveMarkup());
}
