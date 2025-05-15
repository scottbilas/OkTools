namespace OkTools.Terminal;

using Spectre.Console;

public static partial class DocoptUtils
{
    public class FullHelpOptions
    {
        public bool Show;
        public Func<bool /*available*/, string>? AvailableMessage;
        /*
         * if (available)
            var extra = helpArgs.ArgCommand != null ? $" {helpArgs.ArgCommand}": "";
            return $"Full help is available via `{{cliapp.name}} help -f{extra}`"
           else
            var extra = nameOfFullHelpOption != null ? $"; {nameOfFullHelpOption} flag ignored" : null;
            return $"\n\n(This command does not have extra help{extra}.)";
         */
    }

    public class HelpStyles
    {
        public string HeadingStart  = "[underline blue]";
        public string HeadingStop   = "[/]";
        public string OptionStart   = "[yellow]";
        public string OptionStop    = "[/]";
        public string ArgumentStart = "[yellow]";
        public string ArgumentStop  = "[/]";
        public string ChoiceStart   = "[italic]";
        public string ChoiceStop    = "[/]";
        public string ErrorStart    = "[red]";
        public string ErrorStop     = "[/]";
        public string CodeStart     = "[aqua]";
        public string CodeStop      = "[/]";

        public static readonly HelpStyles Default = new();
    }

    public class HelpOptions
    {
        public FullHelpOptions? FullOptions;
        public HelpStyles Styles = new();
        public bool NoStyle;
    }

    public static void DisplayHelp(CliContext cliContext, string helpText, HelpOptions? helpOptions = null)
    {
        var helpLines = cliContext
            .ReplaceMacros(helpText)
            .SelectLinesAsSegments();

        if (helpOptions is { FullOptions: not null })
            helpLines = FilterFullHelp(helpLines, helpOptions.FullOptions);

        if (cliContext.WrapWidth != null)
        {
            var wrapWidth = cliContext.WrapWidth == 0 ? DocoptReflowOptions.DefaultWrapWidth : cliContext.WrapWidth.Value;
            if (cliContext.MaxWrapWidth != null)
                Minimize(ref wrapWidth, cliContext.MaxWrapWidth.Value);

            helpLines = Reflow(helpLines, wrapWidth);
        }

        var styles = helpOptions?.Styles ?? HelpStyles.Default;

        if (helpOptions?.NoStyle != true && cliContext.Colored.SupportsColor)
        {
            var helpArray = helpLines.ToArray();
            foreach (var line in Colorize(helpArray, helpArray.Length > 15, styles))
            {
                try
                {
                    cliContext.Colored.MarkupLine(line);
                }
                catch (InvalidOperationException x)
                {
                    throw new InvalidOperationException($"{x}: {helpText}", x);
                }
            }
        }
        else
        {
            foreach (var line in helpLines)
                cliContext.Status.WriteLine(line.ToString());
        }
    }

    static IEnumerable<StringSegment> FilterFullHelp(IEnumerable<StringSegment> help, FullHelpOptions options)
    {
        var insideFullHelpSection = false;
        var includedFullHelp = false;

        var filtered = help
            .Select(line => line.TrimEnd()) // catch accidental whitespace at end of docopt help text lines
            .SelectWithPositions()
            .Where(line =>
            {
                // full-help section begin marker
                if (line.item == "  <")
                {
                    if (insideFullHelpSection)
                        throw new InvalidOperationException("Full help section already open");
                    insideFullHelpSection = true;
                    includedFullHelp = true;
                    return false;
                }

                // full-help section end marker
                if (line.item == "  >")
                {
                    if (!insideFullHelpSection)
                        throw new InvalidOperationException("Full help section not open");
                    insideFullHelpSection = false;
                    return false;
                }

                if (line.isLast && insideFullHelpSection)
                    throw new InvalidOperationException("Full help section was never closed");

                return !insideFullHelpSection || options.Show;
            })
            .SelectItem();

        return (options.AvailableMessage != null, hadFull: includedFullHelp, options.Show) switch
        {
            (true, true, false) => filtered.Concat([default, new(options.AvailableMessage!(true))]),
            (true, false, true) => filtered.Concat([default, new(options.AvailableMessage!(false))]),
            _ => filtered
        };
    }

    static IEnumerable<string> Colorize(IEnumerable<StringSegment> helpLines, bool isBigHelp, HelpStyles styles)
    {
        var lastLine = new StringSegment();
        var codeOpen = false;

        foreach (var helpLine in helpLines)
        {
            // heading
            if (!helpLine.StartsWith(' ') && !helpLine.StartsWith("Full help", StringComparison.Ordinal))
            {
                yield return helpLine
                    .ToString().EscapeMarkup()
                    .RegexReplace("(.*):(.*)", $"{styles.HeadingStart}$1{styles.HeadingStop}: $2");
            }
            // ordinary text
            else
            {
                // docopt parser needs no blank line after these, but i like it better more spread out.
                // but only if it's "big help"!
                if (!lastLine.StartsWith(' ') && !lastLine.IsEmpty && isBigHelp)
                    yield return "";

                var parts = helpLine.ToString().Split('`');
                var startCodeOpen = codeOpen;
                for (var i = 0; i < parts.Length; ++i)
                {
                    if (i % 2 != 0 == startCodeOpen)
                    {
                        parts[i] = parts[i]
                            .EscapeMarkup()
                            .RegexReplace(// -x and --xyz style options
                                //@"([^a-z]|^)(--[a-z][a-z*-]*|-[a-z])([^a-z]|$)",
                                @"(?<![a-z])(--([a-z][a-z0-9*-]*|\*[a-z0-9-]*)|-[a-z0-9]|--)(?![a-z0-9])",
                                $"{styles.OptionStart}$1{styles.OptionStop}")
                            .RegexReplace(// ARGUMENTS
                                @"\b[A-Z][A-Z0-9]{2,}\b",
                                $"{styles.ArgumentStart}$0{styles.ArgumentStop}")
                            .RegexReplace(// lists of choices
                                @"  \* (.*):  ",
                                $"  * {styles.ChoiceStart}$1:{styles.ChoiceStop}  ")
                            .RegexReplace(// errors
                                " ! (.*) !",
                                $" {styles.ErrorStart}$1{styles.ErrorStop}");
                        codeOpen = false;
                    }
                    else
                    {
                        // odd parts are code
                        parts[i] = styles.CodeStart + parts[i].EscapeMarkup() + styles.CodeStop;
                        codeOpen = true;
                    }
                }

                yield return parts.StringJoin("");
            }

            lastLine = helpLine;
        }
    }
}
