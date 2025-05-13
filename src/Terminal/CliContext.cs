namespace OkTools.Terminal;

using Spectre.Console;
using Terminal = Vezel.Cathode.Terminal;

public enum DebugLevel
{
    Off,
    Normal,
    Extreme,
}

public enum PauseLevel
{
    Off,
    Errors,
    Warnings,
    Always,
}

public enum OutputFormat
{
    Humane,
    Plain,
    Jsonl,
}

public readonly record struct CliCommandSpec(
    string Name,
    string ShortHelp, // empty means hidden
    string Help,
    Func<CliContext, IReadOnlyList<string>, Task<CliExitCode>> Exec);

public class CliContext
{
    // logging
    DebugLevel _debugLevel;
    OutputFormat _outputFormat;
    int? _wrapWidth = 0;
    int? _maxWrapWidth;
    ILogger? _verboseLogger;
    TrackedLogger _warningLogger = null!, _errorLogger = null!;
    IStatusLogger _statusLogger = null!, _coloredLogger = null!;

    // config
    readonly CliAppConfig _config;
    readonly TextUtility.MacroReplacer _macroReplacer;
    NPath? _toolDir;

    public CliContext(CliAppConfig config)
    {
        _config = config;

        try
        {
            _maxWrapWidth = Terminal.Size.Width;
        }
        catch (IOException)
        {
            // this can happen if the console isn't attached (like during early stages of a unit test)
        }

        IEnumerable<SimpleTextMacro> simpleMacros =
        [
            ("cliapp.name", _config.ProgramName),
            ("cliapp.version", _config.ProgramVersion),
        ];

        IEnumerable<SimpleMacroWriter> macroWriters =
        [
            ("commands.help", (_, w) =>
            {
                var minWidth = _config.Commands.Select(s => s.Name.Length).Max();
                w.Write(_config.Commands
                    .Where(c => c.ShortHelp != "")
                    .Select(s => $"  {s.Name.PadRight(minWidth)}  {ReplaceMacros(s.ShortHelp)}")
                    .StringJoin('\n'));
            })
        ];

        if (_config.SimpleMacros != null)
            simpleMacros = simpleMacros.Concat(_config.SimpleMacros);
        if (_config.MacroWriters != null)
            macroWriters = macroWriters.Concat(_config.MacroWriters);

        // TODO: can i fix up the api so it can infer the types?
        var staticMacroReplacer = TextUtility.CreateMacroReplacer(Enumerable.Concat(
            simpleMacros.Select<SimpleTextMacro, (string, Action<TextWriter>)>(
                item => (item.macroName, writer => writer.Write(item.replacement))),
            macroWriters.Select<SimpleMacroWriter, (string, Action<TextWriter>)>(
                item => (item.macroName, writer => item.writeAction(this, writer)))));

        if (_config.MacroReplacer != null)
        {
            _macroReplacer = (name, writer) =>
                staticMacroReplacer(name, writer) || _config.MacroReplacer(name, writer);
        }
        else
            _macroReplacer = staticMacroReplacer;

        InitLogging(default, default);
    }

    public CliAppConfig   Config  => _config;
    public ITrackedLogger Error   => _errorLogger;
    public ITrackedLogger Warning => _warningLogger;
    public IStatusLogger  Status  => _statusLogger;
    public IStatusLogger  Colored => _coloredLogger; // use when we want color output even if redirected. this is meant for help commands, where redirection probably means it's being run through a pager like `less`.
    public ILogger        Verbose => _verboseLogger ?? throw new InvalidOperationException($"Check {nameof(IsVerbose)} before using {nameof(Verbose)}");

    public DebugLevel   DebugLevel   => _debugLevel;
    public OutputFormat OutputFormat => _outputFormat;
    public int?         WrapWidth    => _wrapWidth; // null means "no wrap" (0 means "default wrap")
    public int?         MaxWrapWidth => _maxWrapWidth; // maximum possible wrap, null means "unable to detect" (like if noninteractive or no console)
    public bool         IsVerbose    => DebugLevel > DebugLevel.Off;

    public NPath ToolDir => _toolDir ??= AppContext.BaseDirectory.ToNPath();

    // TODO: this is temporary. implement proper color detection according to https://clig.dev/#output rules on color
    //       (also see Spectre.Console.AnsiDetector for some windows-specific detection code, consider reusing)
    // TODO: if error redirected, modify loggers to prefix every line (\n not just WriteLine) with "Error" or "Warning"
    static (bool status, bool warningError) DetectColorEnabled()
    {
        // override detection for color support because we're deciding this for ourselves
        AnsiConsole.Profile.Capabilities.Ansi = true;

        return (Terminal.StandardOut.IsInteractive, Terminal.StandardError.IsInteractive);
    }

    public void InitLogging(DebugLevel debugLevel, OutputFormat outputFormat)
    {
        _debugLevel = debugLevel;
        _outputFormat = outputFormat;

        if (_outputFormat != OutputFormat.Humane)
            _wrapWidth = null;

        var stdout = Terminal.StandardOut.TextWriter;
        var stderr = Terminal.StandardError.TextWriter;

        if (_outputFormat == OutputFormat.Jsonl)
        {
            _verboseLogger = IsVerbose ? new JsonLogger(stdout, "verbose") : null;
            _statusLogger  = new JsonLogger(stdout, "status");
            _coloredLogger = new JsonLogger(stdout, "status");
            _warningLogger = new TrackedLogger(new JsonLogger(stderr, "warning"));
            _errorLogger   = new TrackedLogger(new JsonLogger(stderr, "error"));
        }
        else if (_outputFormat == OutputFormat.Plain)
        {
            _verboseLogger = IsVerbose ? new TextWriterLogger(stdout) : null;
            _statusLogger  = new StatusWriterLogger(stdout);
            _coloredLogger = new StatusWriterLogger(stdout);
            _warningLogger = new TrackedLogger(new TextWriterLogger(stderr));
            _errorLogger   = new TrackedLogger(new TextWriterLogger(stderr));
        }
        else
        {
            var (status, warningError) = DetectColorEnabled();

            _verboseLogger = IsVerbose
                ? new AnsiConsoleLogger(Color.Grey)
                : null;
            _statusLogger = status
                ? new AnsiStatusWriterLogger(stdout)
                : new StatusWriterLogger(stdout);
            _coloredLogger = new AnsiStatusWriterLogger(stdout);
            _warningLogger = new TrackedLogger(warningError
                ? new AnsiConsoleLogger(Color.Yellow)
                : new TextWriterLogger(stderr));
            _errorLogger = new TrackedLogger(warningError
                ? new AnsiConsoleLogger(Color.Red)
                : new TextWriterLogger(stderr));
        }
    }

    public StringSegment ReplaceMacros(StringSegment text) => TextUtility.ReplaceMacros(text, _macroReplacer);
}
