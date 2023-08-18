using DocoptNet;

// fine to use console directly here, it's outside of any tui work
#pragma warning disable RS0030

// ReSharper disable MethodHasAsyncOverload

class HelpCommandResult : IHelpResult
{
    public string Help { get; }

    public HelpCommandResult(string help) { Help = help.TrimStart(); }
}

static class DocoptExtensions
{
    class InputErrorResult : IInputErrorResult
    {
        public InputErrorResult(string error) { Error = error; }
        public string Error { get; }
        public string Usage => throw new InvalidOperationException(); // using params
    }

    public static (CliExitCode? code, T parsed) Parse<T>(this IHelpFeaturingParser<T> @this,
        IReadOnlyCollection<string> args,
        string programVersion, string help, string usage,
        Func<T, object?>? postParse = null)
    {
        (CliExitCode? code, T parsed) rc = default; // note the T instead of T? because it will never be null if CliExitCode is null

        try
        {
            object result = @this
                .WithVersion(programVersion)
                .Parse(args);

            {
                if (postParse != null && result is IArgumentsResult<T> opts)
                {
                    try
                    {
                        var postResult = postParse(opts.Arguments);
                        if (postResult != null)
                            result = postResult;
                    }
                    catch (DocoptInputErrorException x)
                    {
                        result = new InputErrorResult(x.Message);
                    }
                }
            }

            switch (result)
            {
                case IArgumentsResult<T> opts:
                    rc.parsed = opts.Arguments;
                    break;

                case IHelpResult helpResult:
                    var helpText = FormatHelp(helpResult.Help, programVersion);
                    Console.WriteLine(DocoptUtility.Reflow(helpText, Console.WindowWidth));
                    rc.code = CliExitCode.Help;
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

                    if (errorResult.Error.Length != 0)
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
