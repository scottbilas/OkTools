using DocoptNet;

// fine to use console directly here, it's outside of any tui work
#pragma warning disable RS0030

// ReSharper disable MethodHasAsyncOverload

static class DocoptExtensions
{
    public static async Task<CliExitCode> RunCli<T>(this IHelpFeaturingParser<T> @this,
        IReadOnlyCollection<string> args,
        string programVersion,
        string help,
        string usage,
        Func<T, Task<CliExitCode>> mainFunc)
    {
        try
        {
            var result = @this
                .WithVersion(programVersion)
                .Parse(args);

            switch (result)
            {
                case IArgumentsResult<T> cliArgs:
                {
                    return await mainFunc(cliArgs.Arguments);
                }
                case IHelpResult:
                {
                    var helpText = FormatHelp(help, programVersion);
                    Console.WriteLine(DocoptUtility.Reflow(helpText, Console.WindowWidth));
                    return CliExitCode.Help;
                }
                case IVersionResult:
                {
                    var shortDescription = FormatHelp(help[..help.IndexOf('\n')].Trim(), programVersion);
                    Console.WriteLine(shortDescription);
                    return CliExitCode.Help;
                }
                case IInputErrorResult errorResult:
                {
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

                    return CliExitCode.ErrorUsage;
                }
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
            return CliExitCode.ErrorSoftware;
        }
    }
    
    public static CliExitCode RunCli<T>(this IHelpFeaturingParser<T> @this,
        IReadOnlyCollection<string> args,
        string programVersion,
        string help,
        string usage,
        Func<T, CliExitCode> mainFunc)
    {
        var task = @this.RunCli(args, programVersion, help, usage, cliArgs => Task.FromResult(mainFunc(cliArgs)));
        return task.Result;
    }
}
