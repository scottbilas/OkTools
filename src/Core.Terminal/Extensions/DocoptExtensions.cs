using DocoptNet;

namespace OkTools.Core.Terminal.Extensions;

// ReSharper disable MethodHasAsyncOverload

[PublicAPI]
public static class DocoptExtensions
{
    class InputErrorResult : IInputErrorResult
    {
        public InputErrorResult(string error) { Error = error; }
        public string Error { get; }
        public string Usage => throw new InvalidOperationException(); // using params
    }

    // TODO: fix, i hate this API
    public static (CliExitCode? code, T parsed) Parse<T>(this IHelpFeaturingParser<T> @this,
        IReadOnlyCollection<string> args,
        string programVersion, string help, string usage,
        Func<T, object?>? postParse = null,
        TextWriter? outWriter = null,
        TextWriter? errWriter = null,
        int? wrapWidth = null)
    {
        // vezel warns about this, but if using vezel we should set outWriter/errWriter
#       pragma warning disable RS0030
        outWriter ??= Console.Out;
        errWriter ??= Console.Error;
        wrapWidth ??= !Console.IsOutputRedirected ? Console.WindowWidth : 0;
#       pragma warning restore RS0030

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
                    outWriter.WriteLine(DocoptUtility.Reflow(helpText, wrapWidth.Value));
                    rc.code = CliExitCode.Help;
                    break;

                case IVersionResult:
                    var shortDescription = FormatHelp(help[..help.IndexOf('\n')].Trim(), programVersion);
                    outWriter.WriteLine(shortDescription);
                    rc.code = CliExitCode.Help;
                    break;

                case IInputErrorResult errorResult:
                    var printed = false;

                    if (args.Count != 0)
                    {
                        errWriter.WriteLine("Bad command line: " + args.StringJoin(' '));
                        printed = true;
                    }

                    if (errorResult.Error.Length != 0)
                    {
                        errWriter.WriteLine(errorResult.Error);
                        printed = true;
                    }

                    if (printed)
                        errWriter.WriteLine();

                    var usageText = FormatHelp(usage, programVersion);
                    errWriter.WriteLine(DocoptUtility.Reflow(usageText, wrapWidth.Value));

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
            errWriter.WriteLine("Internal error!");
            errWriter.WriteLine();
            errWriter.WriteLine(x);
            rc.code = CliExitCode.ErrorSoftware;
        }

        return rc;
    }
}
