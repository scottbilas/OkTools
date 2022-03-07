using System.Diagnostics;
using System.Runtime.InteropServices;
using NiceIO;
using OkTools.Core;
using OkTools.Unity;

static partial class Commands
{
    // TODO: partial matches (like ignore hash and go with most recent install)

/*
    * Choose an EXE
        * Match something I already have on my machine (latest beta or released, or give me a chooser)
        * Warnings about debug vs release, or mismatched project causing an upgrade (does Unity do this already? or was it the Hub?)
    * Set env and command line etc.
        * Make Unity's flags visible with tab completion (something like Crescendo would do..except Crescendo doesn't seem to support parameterized exe location..)
        * Support an individual project's extensions to tab completion for any command line flags the project supports
        * PSReadLine support for tabbing through matching discovered unity versions
    * Support the exe run
        * [probably posh-only] Start separate job watching unity.exe and monitoring pmip files from it, holding handle open and copy on process exit
        * Hub killing
        * Log rotation
        * Automatic bringing to front of an existing Unity (avoid Unity's stupid handling of this)
*/

    public static readonly string DocUsageUnity =
@$"Usage: okunity unity [options] [PROJECT] [-- EXTRA...]

Description:
  Run Unity to open the given PROJECT (defaults to '.'). Uses the project version given to find the matching
  Unity toolchain; see `okunity help toolchains` for how toolchains are discovered.

  Any arguments given in EXTRA will be passed along to the newly launched Unity process.

Options:
  --dry-run           Don't change anything, only print out what would happen instead
  --attach-debugger   Unity will pause with a dialog so you can attach a managed debugger
  --rider             Open Rider after the project opens in Unity
  --no-local-log      Disable local log feature; Unity will use global log ({UnityConstants.UnityEditorDefaultLogPath.ToNPath().ToNiceString()})
  --no-burst          Completely disable Burst
  --verbose-upm-logs  Tell Unity Package Manager to write verbose logs ({UnityConstants.UpmLogPath.ToNPath().ToNiceString()})

  All of these options will only apply to the new Unity session being launched.
";

    public static CliExitCode RunUnity(CommandContext context)
    {
        var doit = !context.CommandLine["--dry-run"].IsTrue;

        // TODO: move this into OkTools.Unity once we get a decent (typesafe) config-overlay system. that way, the CLI
        // can just overlay the config with whatever cl options it wants to support and keep all the logic in the lib.
        // posh cmdlet can do exact same thing, with no serious duplicated logic.
        //
        // idea: major util funcs in OkTools.Unity (like RunUnity) would receive an explicit config type. caller has to
        // produce this. cli and posh cmdlet args would be manually converted, but need some kind of DotNetConfig
        // defaults-provider support. maybe leave the dotnetconfig in the utility function. if the config types track
        // when they are overridden, then the dotnetconfig can be used as a default-provider "underlay".

        // get a valid unity project

        UnityProject? unityProject;

        // scope
        {
            var projectPath = new NPath(context.CommandLine["PROJECT"].Value?.ToString() ?? ".");
            if (!projectPath.DirectoryExists())
            {
                Console.Error.WriteLine($"Could not find directory '{projectPath}'");
                return CliExitCode.ErrorNoInput;
            }

            unityProject = UnityProject.TryCreateFromProjectRoot(projectPath);
            if (unityProject == null)
            {
                Console.Error.WriteLine($"Directory is not a Unity project '{projectPath}'");
                return CliExitCode.ErrorNoInput;
            }
        }

        // check if unity is already running on that project

        var existingUnityRc = TryActivateExistingUnity(unityProject, doit);
        if (existingUnityRc != null)
            return existingUnityRc.Value;

        // find a matching toolchain

        var testableVersions = unityProject.GetTestableVersions().Memoize();
        var foundToolchains = FindAllToolchains(context.Config, false).Memoize();

        var unityToolchain = testableVersions
            .WithIndex()
            .SelectMany(v => foundToolchains.Select(t => (version: v.item, vindex: v.index, toolchain: t)))
            .FirstOrDefault(i => i.toolchain.Version == i.version);

        if (unityToolchain == default)
        {
            Console.Error.WriteLine("Could not find a compatible toolchain :(");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Project:");
            Console.Error.WriteLine();
            Output(unityProject, OutputFlags.Yaml, Console.Error);
            Console.Error.WriteLine();
            Console.Error.WriteLine("Available toolchains:");
            Output(foundToolchains.OrderByDescending(t => t.GetInstallTime()), 0, Console.Error);
            return CliExitCode.ErrorUnavailable;
        }

        if (unityToolchain.vindex == 0)
        {
            Console.WriteLine($"Found exact match for project version {unityToolchain.version} in {unityToolchain.toolchain.Path}");
        }
        else
        {
            Console.WriteLine($"Project is version {unityProject.GetVersion()}");
            Console.WriteLine($"Found compatible version {unityToolchain.version} (from {UnityConstants.EditorsYmlFileName}) in {unityToolchain.toolchain.Path}");
        }

        // build up cli and environment

        var unityArgs = new List<string> { "-projectPath", unityProject.Path };
        var unityEnv = new Dictionary<string, object>{ {"UNITY_MIXED_CALLSTACK", 1}, {"UNITY_EXT_LOGGING", 1} };

        if (context.GetConfigBool("unity", "attach-debugger"))
        {
            //TODO status "Will wait for debugger on Unity startup"
            unityEnv.Add("UNITY_GIVE_CHANCE_TO_ATTACH_DEBUGGER", 1);
        }

        if (context.GetConfigBool("unity", "rider"))
        {
            //TODO status "Opening Rider on {slnname} after project open in Unity"
            unityArgs.Add(@"-executeMethod");
            unityArgs.Add("Packages.Rider.Editor.RiderScriptEditor.SyncSolutionAndOpenExternalEditor");
        }

        if (context.GetConfigBool("unity", "no-burst"))
        {
            //TODO status "Disabling Burst"
            unityArgs.Add("--burst-disable-compilation");
        }

        if (context.GetConfigBool("unity", "verbose-upm-logs"))
        {
            //TODO status "Turning on extra debug logging for UPM ({UnityConstants.UpmLogPath.ToNPath().ToNiceString()})"
            unityArgs.Add("-enablePackageManagerTraces");
        }

        if (context.GetConfigBool("unity", "no-local-log"))
        {
            //TODO status "Debug logging to default ({UnityConstants.UnityEditorDefaultLogPath.ToNPath().ToNiceString()})"
        }
        else
        {
            //TODO status "Debug logging to %path"
            //TODO make log pathspec configurable

            var logPath = unityProject.Path.ToNPath()
                .Combine(UnityConstants.ProjectLogsFolderName)
                .EnsureDirectoryExists()
                .Combine($"{unityProject.Name}-editor.log");

            // rotate old log
            // TODO: give option to cap by size and/or file count the logs
            // TODO: give format config for rotation name
            if (logPath.FileExists())
            {
                var targetPath = logPath.ChangeFilename($"{unityProject.Name}-editor_{logPath.FileInfo.CreationTime:yyyyMMdd_HHMMss}.log");
                if (doit)
                    File.Move(logPath, targetPath);
                else
                    Console.WriteLine($"[dryrun] Rotating previous log file {logPath.RelativeTo(unityProject.Path)} to {targetPath.RelativeTo(unityProject.Path)}");
            }

            unityArgs.Add("-logFile");
            unityArgs.Add(logPath);
        }

        if (context.Config.TryGetString("unity", null, "extra-args", out var extraArgs))
            unityArgs.AddRange(CliUtility.ParseCommandLineArgs(extraArgs));
        unityArgs.AddRange(context.CommandLine["EXTRA"].AsStrings());

        // TODO: -vvv style verbose level logging support

        if (doit)
        {
            var unityStartInfo = new ProcessStartInfo
            {
                FileName = unityToolchain.toolchain.EditorExePath,
                WorkingDirectory = unityProject.Path,
            };

            foreach (var arg in unityArgs)
                unityStartInfo.ArgumentList.Add(arg);
            foreach (var (envName, envValue) in unityEnv)
                unityStartInfo.Environment[envName] = envValue.ToString();

            var unityProcess = Process.Start(unityStartInfo);
            if (unityProcess == null)
                throw new Exception($"Unexpected failure to start process '{unityStartInfo.FileName}'");

            Console.WriteLine("Launched Unity as pid " + unityProcess.Id);
        }
        else
        {
            Console.WriteLine("[dryrun] Executing Unity with:");
            Console.WriteLine("[dryrun]   path        | " + unityToolchain.toolchain.EditorExePath);
            Console.WriteLine("[dryrun]   arguments   | " + CliUtility.CommandLineArgsToString(unityArgs));
            Console.WriteLine("[dryrun]   environment | " + unityEnv.Select(kvp => $"{kvp.Key}={kvp.Value}").StringJoin("; "));
        }

        return CliExitCode.Success;
    }

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    static CliExitCode? TryActivateExistingUnity(UnityProject project, bool doit)
    {
        var unityProcesses = Unity.FindUnityProcessesForProject(project.Path);
        if (!unityProcesses.Any())
            return null;

        try
        {
            Console.WriteLine("Unity already running on project with process id "
                              + unityProcesses.Select(p => p.Id).StringJoin(", "));

            var mainUnity = Unity.TryFindMainUnityProcess(unityProcesses);
            if (mainUnity == null)
            {
                Console.Error.WriteLine("Unable to find Unity main process window, best we just abort while you sort it out..");
                return CliExitCode.ErrorUnavailable;
            }

            Console.WriteLine($"Main Unity process ({mainUnity.Id}) discovered with title \"{mainUnity.MainWindowTitle}\"");

            if (!mainUnity.Responding)
            {
                Console.Error.WriteLine("Process is not responding, best we just abort while you sort it out..");
                return CliExitCode.ErrorUnavailable;
            }

            Console.WriteLine("Process is responding, activating and bringing to the front!");

            if (doit)
                SetForegroundWindow(mainUnity.MainWindowHandle);

            return CliExitCode.Success;
        }
        finally
        {
            foreach (var unityProcess in unityProcesses)
                unityProcess.Dispose();
        }
    }
}
