using DocoptNet;
using NiceIO;
using OkTools.Core;

// TODO: this is temporary until remove Console.* calls
#pragma warning disable RS0030

class CliExitException : Exception
{
    public CliExitException(string message, CliExitCode code) : base(message) { Code = code; }
    public CliExitException(string message, Exception innerException, CliExitCode code) : base(message, innerException) { Code = code; }

    public readonly CliExitCode Code;
}

public static partial class Program
{
    const string k_programName = "okflog";
    const string k_docName = $"{k_programName}, the logfile analyzer";
    const string k_docVersion = "0.1";

    // TODO: add --config prefix before command

    const string k_docUsage = $@"{k_docName}

Usage:
  {k_programName}  [options] PATH
  {k_programName}  --version

Options:
  --debug  Enable extra debug features
";

    public static async Task<int> Main(string[] args)
    {
        var debugMode = false;

        try
        {
            if (args.Length == 0)
                throw new DocoptExitException(k_docUsage);

            var options = new Docopt().Apply(k_docUsage, args, version: $"{k_docName} {k_docVersion}", help: false);
            debugMode = options["--debug"].IsTrue;

            using var flogApp = new FlogApp(options);
            return (int)await flogApp.Run();
        }
        catch (DocoptInputErrorException x)
        {
            Console.Error.WriteLine("bad command line: " + args.StringJoin(' '));
            Console.Error.WriteLine(x.Message);
            return (int)CliExitCode.ErrorUsage;
        }
        catch (DocoptExitException x)
        {
            Console.WriteLine(DocoptUtility.Reflow(x.Message, Console.WindowWidth));
            return (int)CliExitCode.Help;
        }
        catch (TerminalNotInteractiveException)
        {
            Console.Error.WriteLine("this app requires an interactive terminal");
            return (int)CliExitCode.ErrorUsage;
        }
        catch (Exception x)
        {
            if (x is CliExitException clix)
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

                return (int)clix.Code;
            }

            Console.Error.WriteLine("Internal error!");
            Console.Error.WriteLine();
            Console.Error.WriteLine(x);
            return (int)CliExitCode.ErrorSoftware;
        }
    }
}
