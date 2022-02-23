using NiceIO;
using OkTools.Core;
using OkTools.Unity;

static partial class Commands
{
    // TODO: partial matches (like ignore hash and go with most recent install)
    // TODO: --toolchain that forces (but will also warn if not match) use of a certain toolchain
    // TODO: --version-override that will override the GetTestableVersions() query with the given value

    public static readonly string DocUsageUnity =
@$"Usage: okunity unity [options] [PROJECT]

Description:
  Run Unity to open the given PROJECT (defaults to '.'). Uses the project version given to find the matching
  Unity toolchain; see `okunity help toolchains` for how toolchains are discovered.

Options:
  --dry-run           Don't change anything, only print out what would happen instead
  --attach-debugger   Unity will pause with a dialog and so you can attach a managed debugger
  --rider             Open Rider after the project opens in Unity
  --no-local-log      Disable local log feature; Unity will use global log ({UnityConstants.UnityEditorDefaultLogPath.ToNPath().ToNiceString()})
  --no-burst          Completely disable Burst
  --verbose-upm-logs  Tell Unity Package Manager to write verbose logs ({UnityConstants.UpmLogPath.ToNPath().ToNiceString()})

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

        var testableVersions = project.GetTestableVersions().Memoize();
        var foundToolchains = FindAllToolchains(context.Config, false).Memoize();

        var match = testableVersions
            .WithIndex()
            .SelectMany(v => foundToolchains.Select(t => (version: v.item, vindex: v.index, toolchain: t)))
            .FirstOrDefault(i => i.toolchain.Version == i.version);

        if (match == default)
        {
            Console.Error.WriteLine("Could not find a compatible toolchain :(");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Project:");
            Console.Error.WriteLine();
            Output(project, OutputFlags.Yaml, Console.Error);
            Console.Error.WriteLine();
            Console.Error.WriteLine("Available toolchains:");
            Output(foundToolchains.OrderByDescending(t => t.GetInstallTime()), 0, Console.Error);
            return CliExitCode.ErrorUnavailable;

        }

        Console.WriteLine(match.vindex == 0
            ? $"Found exact match for project version {match.version} at {match.toolchain.Path}"
            : $"Found compatible match for project version {match.version} (from {UnityConstants.EditorsYmlFileName}) at {match.toolchain.Path}");

        // build up cli

        var args = new List<string>();

        if (context.GetConfigBool("unity", "rider"))
        {
            //TODO status "Opening Rider on {slnname} after project open in Unity"
            args.Add(@"-executeMethod");
            args.Add("Packages.Rider.Editor.RiderScriptEditor.SyncSolutionAndOpenExternalEditor");
        }

        if (context.GetConfigBool("unity", "no-burst"))
        {
            //TODO status "Disabling Burst"
            args.Add("--burst-disable-compilation");
        }

        if (context.GetConfigBool("unity", "verbose-upm-logs"))
        {
            //TODO status "Turning on extra debug logging for UPM ({UnityConstants.UpmLogPath.ToNPath().ToNiceString()})"
            args.Add("-enablePackageManagerTraces");
        }

        if (context.GetConfigBool("unity", "no-local-log"))
        {
            //TODO status "Debug logging to default ({UnityConstants.UnityEditorDefaultLogPath.ToNPath().ToNiceString()})"
        }
        else
        {
            //TODO status "Debug logging to %path"

            //TODO make log pathspec configurable
            var logPath = project.Path.ToNPath()
                .Combine(UnityConstants.ProjectLogsFolderName)
                .EnsureDirectoryExists()
                .Combine($"{project.Name}-editor.log");

            // rotate old log
            // TODO: give option to cap by size and/or file count the logs
            // TODO: give format config for rotation name
            /*
            if (logPath.FileExists())
            {
                var targetBase = logPath.ChangeExtension($"{project.Name}-editor_{logPath.FileInfo.LastWriteTime:yyyyMMdd_HHMMss}");
                Move-Item $logFile "$targetBase.log"
            }
            var logFile = Get-Item -ea:silent $logPath


            $unityArgs += '-logFile', $logPath*/
        }

        // TODO: build up cli, dryrun, proc launching
        // TODO: -vvv style verbose level logging support

        /*
        # TODO: check to see if a unity already running for that path. either activate if identical to the one we want (and command line we want)
        # or abort if different with warnings.

        if ($PSCmdlet.ShouldProcess("$($info.UnityExe) $unityArgs", "Running Unity")) {
            $oldAttach = $Env:UNITY_GIVE_CHANCE_TO_ATTACH_DEBUGGER
            $oldMixed = $Env:UNITY_MIXED_CALLSTACK
            $oldExtLog = $Env:UNITY_EXT_LOGGING
            try {
                if ($AttachDebugger) {
                    $Env:UNITY_GIVE_CHANCE_TO_ATTACH_DEBUGGER = 1
                }

                # always want these features
                $Env:UNITY_MIXED_CALLSTACK = 1
                $Env:UNITY_EXT_LOGGING = 1

                & $info.UnityExe @unityArgs
            }
            finally {
                $Env:UNITY_GIVE_CHANCE_TO_ATTACH_DEBUGGER = $oldAttach
                $Env:UNITY_MIXED_CALLSTACK = $oldMixed
                $Env:UNITY_EXT_LOGGING = $oldExtLog
            }
        }
        */

        return CliExitCode.Success;
    }
}
