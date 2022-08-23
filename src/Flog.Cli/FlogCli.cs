using DocoptNet;

// TODO: this is temporary until remove Console.* calls
#pragma warning disable RS0030

const string programVersion = "0.1";

// TODO: add --config prefix before command

var debugMode = false;

try
{
    var result = FlogCliArguments.CreateParser()
        .WithVersion(programVersion)
        .Parse(args);

    switch (result)
    {
        case IArgumentsResult<FlogCliArguments> cliArgs:
        {
            debugMode = cliArgs.Arguments.OptDebug;

            using var flogApp = new FlogApp(cliArgs.Arguments);
            return (int)await flogApp.Run();
        }
        case IHelpResult:
        {
            var helpText = FormatHelp(FlogCliArguments.Help);
            Console.WriteLine(DocoptUtility.Reflow(helpText, Console.WindowWidth));
            return (int)CliExitCode.Help;
        }
        case IVersionResult:
        {
            var shortDescription = FormatHelp(FlogCliArguments.Help[..FlogCliArguments.Help.IndexOf('\n')].Trim());
            Console.WriteLine(shortDescription);
            return (int)CliExitCode.Help;
        }
        case IInputErrorResult errorResult:
        {
            var printed = false;

            if (args.Length != 0)
            {
                Console.Error.WriteLine("Bad command line: " + args.StringJoin(' '));
                printed = true;
            }

            if (errorResult.Error.Any())
            {
                Console.Error.WriteLine(errorResult.Error);
                printed = true;
            }

            if (printed)
                Console.Error.WriteLine();

            var usageText = FormatHelp(FlogCliArguments.Usage);
            Console.Error.WriteLine(DocoptUtility.Reflow(usageText, Console.WindowWidth));

            return (int)CliExitCode.ErrorUsage;
        }
        default:
            throw new InvalidOperationException($"Unexpected result type {result.GetType().FullName}");
    }

    static string FormatHelp(string helpText)
    {
        var programName = Path.GetFileNameWithoutExtension(Environment.ProcessPath!);
        return string.Format(helpText, programName, programVersion);
    }
}
catch (CliExitException x)
{
    if (x.InnerException != null)
    {
        Console.Error.WriteLine(x.Message);
        if (debugMode)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(x.InnerException);
        }
    }
    else
        Console.Error.WriteLine(x);

    return (int)x.Code;
}
catch (Exception x)
{
    Console.Error.WriteLine("Internal error!");
    Console.Error.WriteLine();
    Console.Error.WriteLine(x);
    return (int)CliExitCode.ErrorSoftware;
}
