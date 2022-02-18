﻿using DocoptNet;
using DotNetConfig;
using OkTools.Core;

public class Program
{
    const string k_docName = "okunity, the Unity CLI toolbox";
    const string k_docVersion = "0.1";

    const string k_docUsageGlobal =
$@"{k_docName}

Usage:
  okunity [options] COMMAND [ARG]...
  okunity --version

Commands:
  help        Print help for a command
  toolchains  Get info on Unity toolchains
  projects    Get info on Unity projects
  info        Extract Unity-related info from args

Global Options:
  --debug     Enable extra debug features
";

    const string k_docUsageHelp =
@"usage: okunity help COMMAND

Print help for COMMAND.
";

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

                var opt = new Docopt().Apply(usage, args[offset..], version: $"{k_docName} {k_docVersion}", optionsFirst: first, help: false);
                first = false;
                return opt;
            }

            if (args.Length == 0) throw new DocoptExitException(k_docUsageGlobal);
            var optGlobal = ParseOpt(k_docUsageGlobal);
            debugMode = optGlobal["--debug"].IsTrue;

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
                            throw new DocoptExitException(Commands.DocUsageToolchains);
                        case "projects":
                            throw new DocoptExitException(Commands.DocUsageProjects);
                        case "info":
                            throw new DocoptExitException(Commands.DocUsageInfo);
                        default:
                            throw new DocoptInputErrorException($"Unknown command '{helpCommand}'");
                    }
                    break;

                case "toolchains":
                    Commands.Toolchains(new CommandContext(ParseOpt(Commands.DocUsageToolchains), Config.Build(), debugMode));
                    break;

                case "projects":
                    Commands.Projects(new CommandContext(ParseOpt(Commands.DocUsageProjects), Config.Build(), debugMode));
                    break;

                case "info":
                    Commands.Info(new CommandContext(ParseOpt(Commands.DocUsageInfo), Config.Build(), debugMode));
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
        catch (Exception x)
        {
            if (debugMode)
                throw;

            Console.Error.WriteLine($"Internal error: {x.Message}");
        }

        return 0;
    }
}
