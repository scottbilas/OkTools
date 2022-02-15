using DocoptNet;
using DotNetConfig;
using OkTools.Core;

public class Program
{
    const string k_docName = "okunity, the Unity CLI toolbox";
    const string k_docVersion = "0.1";

    const string k_docUsageGlobal = k_docName + @"

usage:
  okunity COMMAND [ARG...]
  okunity --version

commands:
  help        Print help for a command
  toolchains  Get info on known toolchains installed locally
  info        Extract Unity-related info from args
";

    const string k_docUsageHelp =
@"usage: okunity help COMMAND

Print help for COMMAND.
";

    const string k_docUsageToolchains =
@"usage: okunity toolchains [-n] [-i SPEC]... [--json]

options:
  -n, --no-defaults   Don't look in the default-install locations for toolchains
  -i, --include SPEC  Add a pathspec to the search (supports '*' and '**' glob style wildcards)
  --json              Write full results as a JSON array
";

    const string k_docUsageInfo =
@"usage: okunity info [--json] [THING]...

  THING  The thing to extract as much unity-related info from as possible.
         Defaults to '.' (the current directory). Currently supported:

         * Path to a folder or file (globs supported)
         * A text version number
";

    public static int Main(string[] args)
    {
        try
        {
            var first = true; // required for COMMAND [ARG...] to work
            IDictionary<string, ValueObject> ParseOpt(string usage)
            {
                var opt = new Docopt().Apply(usage, args, version: $"{k_docName} {k_docVersion}", optionsFirst: first, help: false);
                first = false;
                return opt;
            }

            if (args.Length == 0) throw new DocoptExitException(k_docUsageGlobal);
            var optGlobal = ParseOpt(k_docUsageGlobal);

            // TODO: move config into lib (and cache in static..eventually will need a way to manually refresh on resident gui app on focus; see how vscode does this with editorconfig)
            // TODO: add general CLI override for config (override needs to be applied and stored, so a refresh can have cli overloads reapplied on top)

            var command = optGlobal["COMMAND"].Value;
            switch (command)
            {
                case "help":
                    if (args.Length == 1) throw new DocoptExitException(k_docUsageHelp);
                    var optHelp = ParseOpt(k_docUsageHelp);

                    var helpCommand = optHelp["COMMAND"].Value;
                    switch (helpCommand)
                    {
                        case "help":
                            break;
                        case "toolchains":
                            throw new DocoptExitException(k_docUsageToolchains);
                        case "info":
                            throw new DocoptExitException(k_docUsageInfo);
                        default:
                            throw new DocoptInputErrorException($"Unknown command '{helpCommand}'");
                    }
                    break;

                case "toolchains":
                    Commands.Toolchains(ParseOpt(k_docUsageToolchains), Config.Build());
                    break;

                case "info":
                    Commands.Info(ParseOpt(k_docUsageInfo), Config.Build());
                    break;

                default:
                    throw new DocoptInputErrorException($"Unknown command '{command}'");
            }
        }
        catch (DocoptInputErrorException x)
        {
            Console.Error.WriteLine("bad command line: " + args.StringJoin(' '));
            Console.WriteLine(x.Message);
            return (int)CliExitCode.ErrorUsage;
        }
        catch (DocoptExitException x)
        {
            Console.WriteLine(x.Message);
            return (int)CliExitCode.Help;
        }

        return 0;
    }
}
