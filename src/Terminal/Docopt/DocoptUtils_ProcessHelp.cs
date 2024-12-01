namespace OkTools.Terminal;

using Spectre.Console;

public static partial class DocoptUtils
{
    public class FullHelpOptions
    {
        public required bool ShowFullHelp;
        public Func<bool /*available*/, string>? FullHelpAvailableMessage;
        /*
         * if (available)
            var extra = helpArgs.ArgCommand != null ? $" {helpArgs.ArgCommand}": "";
            return $"Full help is available via `{{cliapp.name}} help -f{extra}`"
           else
            var extra = nameOfFullHelpOption != null ? $"; {nameOfFullHelpOption} flag ignored" : null;
            return $"\n\n(This command does not have extra help{extra}.)";
         */
    }

    public static void DisplayHelp(CliContext cliContext, string helpText, FullHelpOptions? fullHelpOptions = null)
    {
        var helpLines = cliContext
            .ReplaceMacros(helpText)
            .SelectLinesAsSegments();

        if (fullHelpOptions != null)
            helpLines = FilterHelp(helpLines, fullHelpOptions);

        if (cliContext.WrapWidth != null)
        {
            var wrapWidth = cliContext.WrapWidth == 0 ? DocoptReflowOptions.DefaultWrapWidth : cliContext.WrapWidth.Value;
            if (cliContext.MaxWrapWidth != null)
                wrapWidth.MinimizeWith(cliContext.MaxWrapWidth.Value);

            helpLines = Reflow(helpLines, wrapWidth);
        }

        if (cliContext.Colored.SupportsColor)
        {
            var helpArray = helpLines.ToArray();
            foreach (var line in Colorize(helpArray, helpArray.Length > 15))
                cliContext.Colored.MarkupLine(line);
        }
        else
        {
            foreach (var line in helpLines)
                cliContext.Status.WriteLine(line.ToString());
        }
    }

    static IEnumerable<StringSegment> FilterHelp(IEnumerable<StringSegment> help, FullHelpOptions options)
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

                return !insideFullHelpSection || options.ShowFullHelp;
            })
            .SelectItem();

        return (options.FullHelpAvailableMessage != null, hadFull: includedFullHelp, options.ShowFullHelp) switch
        {
            (true, true, false) => filtered.Concat([default, new(options.FullHelpAvailableMessage!(true))]),
            (true, false, true) => filtered.Concat([default, new(options.FullHelpAvailableMessage!(false))]),
            _ => filtered
        };
    }

    static IEnumerable<string> Colorize(IEnumerable<StringSegment> helpLines, bool isBigHelp)
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
                    .RegexReplace("(.*):(.*)", "[underline blue]$1[/]: $2");
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
                                "[yellow]$1[/]")
                            .RegexReplace(// ARGUMENTS
                                @"\b[A-Z][A-Z0-9]{2,}\b",
                                "[yellow]$0[/]")
                            .RegexReplace(// lists of choices
                                @"  \* (.*):  ",
                                "  * [italic]$1:[/]  ")
                            .RegexReplace(// errors
                                " ! (.*) !",
                                " [red]$1[/]");
                        codeOpen = false;
                    }
                    else
                    {
                        // odd parts are code
                        parts[i] = "[fuchsia]" + parts[i].EscapeMarkup() + "[/]";
                        codeOpen = true;
                    }
                }

                yield return parts.StringJoin("");
            }

            lastLine = helpLine;
        }
    }
}
