using DocoptNet;

// fine to use console directly here, it's outside of any tui work
#pragma warning disable RS0030

// ReSharper disable MethodHasAsyncOverload

static class DocoptExtensions
{
    public static (CliExitCode? code, T parsed) Parse<T>(this IHelpFeaturingParser<T> @this,
        IReadOnlyCollection<string> args,
        string programVersion, string help, string usage,
        Func<T, bool>? isHelpCommandSet = null)
    {
        (CliExitCode? code, T parsed) rc = default; // note the T instead of T? because it will never be null if CliExitCode is null

        try
        {
            var result = @this
                .WithVersion(programVersion)
                .Parse(args);

            var doHelp = false;
            if (result is IArgumentsResult<T> cliArgs)
            {
                if (isHelpCommandSet?.Invoke(cliArgs.Arguments) == true)
                    doHelp = true;
                else
                    rc.parsed = cliArgs.Arguments;
            }

            switch (result)
            {
                case IArgumentsResult<T>: // already handled
                    break;

                case IHelpResult:
                    doHelp = true;
                    break;

                case IVersionResult:
                    var shortDescription = FormatHelp(help[..help.IndexOf('\n')].Trim(), programVersion);
                    Console.WriteLine(shortDescription);
                    rc.code = CliExitCode.Help;
                    break;

                case IInputErrorResult errorResult:
                    var printed = false;

                    if (args.Count != 0)
                    {
                        Console.Error.WriteLine("Bad command line: " + args.StringJoin(' '));
                        printed = true;
                    }

                    if (Enumerable.Any(errorResult.Error))
                    {
                        Console.Error.WriteLine(errorResult.Error);
                        printed = true;
                    }

                    if (printed)
                        Console.Error.WriteLine();

                    var usageText = FormatHelp(usage, programVersion);
                    Console.Error.WriteLine(DocoptUtility.Reflow(usageText, Console.WindowWidth));

                    rc.code = CliExitCode.ErrorUsage;
                    break;

                default:
                    throw new InvalidOperationException($"Unexpected result type {result.GetType().FullName}");
            }

            if (doHelp)
            {
                var helpText = FormatHelp(help, programVersion);
                Console.WriteLine(DocoptUtility.Reflow(helpText, Console.WindowWidth));
                rc.code = CliExitCode.Help;
            }

            static string FormatHelp(string helpText, string version)
            {
                var programName = Path.GetFileNameWithoutExtension(Environment.ProcessPath!);
                return string.Format(helpText, programName, version);
            }
        }
        catch (Exception x)
        {
            Console.Error.WriteLine("Internal error!");
            Console.Error.WriteLine();
            Console.Error.WriteLine(x);
            rc.code = CliExitCode.ErrorSoftware;
        }

        return rc;
    }
}
