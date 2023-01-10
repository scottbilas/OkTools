using DocoptNet;
using DotNetConfig;

public static class Program
{
    const string k_docName = "okunity, the Unity CLI toolbox";
    const string k_docVersion = "0.1";

    // TODO: add --config prefix before command
    // TODO: make this pluggable by scanning dll's and using attrs to get the Run function, command name, short and long docs.
    // (don't do git-style fork process because startup costs)

    static readonly string k_docUsageGlobal;

    static Program()
    {
        var minWidth = k_commandSpecs.Select(s => s.Name.Length).Max();
        var docUsageCommands = k_commandSpecs
            .Select(s => $"  {s.Name.PadRight(minWidth)}  {s.ShortDoc}")
            .StringJoin('\n');

        k_docUsageGlobal =
$@"{k_docName}

Usage:
  okunity [options] COMMAND [ARG]...
  okunity --version

Commands:
{docUsageCommands}

Global Options:
  --debug     Enable extra debug features
";
    }

    const string k_docUsageHelp =
@"usage: okunity help COMMAND

Print help for COMMAND.
";

    readonly record struct CommandSpec(string Name, string ShortDoc, string FullDoc, Func<CommandContext, CliExitCode> RunAction);

    static readonly CommandSpec[] k_commandSpecs =
    {
        new("help",       "Print help for any of these commands", k_docUsageHelp, _ => throw new DocoptExitException(k_docUsageHelp)),
        new("unity",      "Run Unity to open a project",          Commands.DocUsageUnity, Commands.RunUnity),
        new("install",    "Install a Unity toolchain",            Commands.DocUsageInstall, Commands.RunInstall),
        new("toolchains", "Get info on Unity toolchains",         Commands.DocUsageToolchains, Commands.RunToolchains),
        new("projects",   "Get info on Unity projects",           Commands.DocUsageProjects, Commands.RunProjects),
        new("assetdb",    "Do things with the asset database",    Commands.DocUsageAssetDb, Commands.RunAssetDb),
        new("info",       "Extract Unity-related info from args", Commands.DocUsageInfo, Commands.RunInfo),
        new("do",         "Dumping ground for random commands",   Commands.DocUsageDo, Commands.RunDo),
        //new("purge", // TODO: purge (logs artifacts shaders, everything, nuclear) (with auto warning if any processes running on the project)
    };

    public static int Main(string[] args)
    {
        var debugMode = false;

        try
        {
            var first = true; // required for COMMAND [ARG...] to work
            IDictionary<string, ValueObject> ParseOpt(string usage)
            {
                // skip global options when parsing for commands, as they are applied before the command
                var offset = first ? 0 : args.IndexOf(a => a[0] != '-');

                var opt = new Docopt().Apply(usage, args[offset..], version: $"{k_docName} {k_docVersion}", optionsFirst: first, help: false)!;
                first = false;
                return opt;
            }

            if (args.Length == 0)
                throw new DocoptExitException(k_docUsageGlobal);

            var optGlobal = ParseOpt(k_docUsageGlobal);
            debugMode = optGlobal["--debug"].IsTrue;

            // TODO: move config into lib (and cache in static..eventually will need a way to manually refresh on resident gui app on focus; see how vscode does this with editorconfig)
            // TODO: add general CLI override for config (override needs to be applied and stored, so a refresh can have cli overloads reapplied on top)

            var mainCommand = optGlobal["COMMAND"].Value?.ToString();
            if (!k_commandSpecs.TryFirst(s => s.Name == mainCommand, out var mainFound))
                throw new DocoptInputErrorException($"Unknown command '{mainCommand}'"); // TODO: "did you mean ...?" :)

            if (mainCommand == "help")
            {
                if (args.Length == 1)
                    throw new DocoptExitException(k_docUsageGlobal);

                var helpCommand = ParseOpt(k_docUsageHelp)["COMMAND"].Value!.ToString()!;
                throw k_commandSpecs.TryFirst(s => s.Name == helpCommand, out var helpFound)
                    ? new DocoptExitException(helpFound.FullDoc)
                    : new DocoptInputErrorException($"Unknown command '{helpCommand}'");
            }

            var commandContext = new CommandContext(mainFound.Name, ParseOpt(mainFound.FullDoc), Config.Build(), debugMode);
            return (int)mainFound.RunAction(commandContext);
        }
        catch (DocoptInputErrorException x)
        {
            Console.Error.WriteLine("bad command line: " + args.StringJoin(' '));
            Console.WriteLine(x.Message);
            return (int)CliExitCode.ErrorUsage;
        }
        catch (DocoptExitException x)
        {
            Console.WriteLine(DocoptUtility.Reflow(x.Message, Console.WindowWidth));
            return (int)CliExitCode.Help;
        }
        catch (Exception x)
        {
            if (debugMode)
                throw;

            Console.Error.WriteLine("Internal error!");
            Console.Error.WriteLine();
            Console.Error.WriteLine(x);

            return (int)CliExitCode.ErrorSoftware;
        }
    }
}
