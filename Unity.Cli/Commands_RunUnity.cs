using NiceIO;
using OkTools.Core;
using OkTools.Unity;

static partial class Commands
{
    // TODO: partial matches (like ignore hash and go with most recent install)

    public static readonly string DocUsageUnity =
@$"Usage: okunity unity [options] [PROJECT]

Description:
  Run Unity to open the given PROJECT (defaults to '.'). Uses the project version given to find the matching
  Unity toolchain; see `okunity help toolchains` for how toolchains are discovered.

Options:
  --dry-run           Don't change anything, only print out what would happen instead
  --attach-debugger   Unity will pause with a dialog and so you can attach a managed debugger
  --rider             Open Rider after the project opens in Unity
  --no-local-log      Disable local log feature; Unity will use global log ({UnityConstants.UnityEditorDefaultLogPath.ToNPath().TildeCollapse()})
  --no-burst          Completely disable Burst
  --verbose-upm-logs  Tell Unity Package Manager to write verbose logs ({UnityConstants.UpmLogPath.ToNPath().TildeCollapse()})

  All of these options will only apply to the new Unity session being launched.
";

    public static CliExitCode RunUnity(CommandContext context)
    {
        // get a valid unity project

        var projectPath = new NPath(context.CommandLine["PROJECT"].Value?.ToString() ?? ".");
        if (!projectPath.DirectoryExists())
        {
            Console.Error.WriteLine($"Could not find directory '{projectPath}'");
            return CliExitCode.ErrorNoInput;
        }

        var project = UnityProject.TryCreateFromProjectRoot(projectPath);
        if (project == null)
        {
            Console.Error.WriteLine($"Directory is not a Unity project '{projectPath}'");
            return CliExitCode.ErrorNoInput;
        }

        // find a matching toolchain

        var versions = project.GetTestableUnityVersions().Memoize();
        var toolchains = FindAllToolchains(context.Config, false).Memoize();

        UnityToolchain? Match()
        {
            foreach (var (version, index) in versions.WithIndex())
            {
                foreach (var toolchain in toolchains)
                {
                    if (toolchain.Version == version)
                    {
                        Console.WriteLine(index == 0
                            ? $"Found exact match for project version {version} at {toolchain.Path}"
                            : $"Found compatible match for project version {version} (from {UnityConstants.EditorsYmlFileName}) at {toolchain.Path}");
                        return toolchain;
                    }
                }
            }

            return null;
        }

        var matchingToolchain = Match();
        if (matchingToolchain == null)
        {
            Console.Error.WriteLine("Could not find a compatible toolchain :(");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Project:");
            Console.Error.WriteLine();
            Output(project, OutputFlags.Yaml, Console.Error);
            Console.Error.WriteLine();
            Console.Error.WriteLine("Available toolchains:");
            Output(toolchains.OrderByDescending(t => t.GetInstallTime()), 0, Console.Error);
            return CliExitCode.ErrorUnavailable;
        }

        // TODO: build up cli, dryrun, proc launching
        // TODO: -vvv style verbose level logging support

        return CliExitCode.Success;
    }
}
