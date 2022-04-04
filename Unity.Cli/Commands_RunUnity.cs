using System.Diagnostics;
using System.Runtime.InteropServices;
using DocoptNet;
using NiceIO;
using OkTools.Core;
using OkTools.Unity;

static partial class Commands
{
    // TODO: partial matches (like ignore hash and go with most recent install)
    // TODO: option to kill unity if found running (perhaps only if not responding..otherwise give it a quit message and a 15s or whatever timeout, then fall back to kill)
    // TODO: --create-project option (probably should also require --toolchain..difficult to choose a default)
    // TODO: --kill-hub option
    // TODO: let --toolchain specify another project path or a projectversion so can mean "launch project x with the same unity as project y uses"

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
  --dry-run               Don't change anything, only print out what would happen instead
  --toolchain TOOLCHAIN   Ignore project version and use this toolchain (can be version, path to toolchain, or unityhub link)
  --scene SCENE           After loading the project, also load this specific scene (creates or overwrites {UnityProjectConstants.LastSceneManagerSetupPath.ToNPath().ToNiceString()})
  --rider                 Open Rider after the project opens in Unity
  --enable-debugger       Enable managed code debugging (disabled optimizations)
  --wait-attach-debugger  Unity will pause with a dialog so you can attach a managed debugger
  --enable-coverage       Enable code coverage
  --stack-trace-log TYPE  Override Unity settings to use the given stack trace level for logs (TYPE can be None, ScriptOnly, or Full)
  --no-local-log          Disable local log feature; Unity will use global log ({UnityConstants.UnityEditorDefaultLogPath.ToNPath().ToNiceString()})
  --no-burst              Completely disable Burst
  --no-activate-existing  Skip normal behavior of activating an existing Unity main window if found running on the project
  --verbose-upm-logs      Tell Unity Package Manager to write verbose logs ({UnityConstants.UpmLogPath.ToNPath().ToNiceString()})

  All of these options will only apply to the new Unity session being launched.

Log Files:
  Unity is always directed to include timestamps in its log files, which will have this format:
    <year>-<month>-<day>T<hour>:<minute>:<second>.<milliseconds>Z|<thread-id>|<log-line>
  The timestamp is unfortunately recorded in unadjusted UTC.

  Also, unless --no-local-log is used, Unity log files will be stored as `Logs/<ProjectName>-editor.log` local to the
  project. Any existing file with this name will be rotated out to a filename with a timestamp appended to it, thus
  preserving logs from previous sessions.
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

        // TODO: warn if the unity running on it is a different version from detected (whether it's not part of GetTestableVersions(), or mismatches a direct --toolchain config)

        var existingUnityRc = TryActivateExistingUnity(unityProject, doit && !context.GetConfigBool("no-activate-existing"));
        if (existingUnityRc != null)
        {
            // TODO: warn if the existing unity process received different command line flags or env than we were going to use here
            return existingUnityRc.Value;
        }

        // find a matching toolchain

        UnityToolchain useToolchain;

        if (context.TryGetConfigString("toolchain", out var useToolchainConfig))
        {
            // given explicitly

            // try as path
            var testToolchain = UnityToolchain.TryCreateFromPath(useToolchainConfig);
            if (testToolchain == null)
            {
                // try as version
                var testVersion = UnityVersion.TryFromText(useToolchainConfig);
                if (testVersion == null)
                {
                    Console.Error.WriteLine($"TOOLCHAIN given ('{useToolchainConfig}') cannot be parsed to a valid toolchain path or Unity version");
                    return CliExitCode.ErrorUsage;
                }

                testToolchain = FindAllToolchains(context.Config, false).FirstOrDefault(t => t.Version.IsMatch(testVersion));
                if (testToolchain == null)
                {
                    Console.Error.WriteLine($"Unable to find any toolchain with version {testVersion}");
                    return CliExitCode.ErrorUnavailable;
                }
            }

            useToolchain = testToolchain;
            Console.WriteLine($"Using toolchain with version {useToolchain.Version} at {useToolchain.Path}");
        }
        else
        {
            // detect from project

            var testableVersions = unityProject.GetTestableVersions().Memoize();
            var foundToolchains = FindAllToolchains(context.Config, false).Memoize();

            var detectedToolchain = testableVersions
                .WithIndex()
                .SelectMany(v => foundToolchains.Select(t => (version: v.item, vindex: v.index, toolchain: t)))
                .FirstOrDefault(i => i.toolchain.Version == i.version);

            if (detectedToolchain == default)
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

            if (detectedToolchain.vindex == 0)
            {
                Console.WriteLine($"Found exact match for project version {detectedToolchain.version} in {detectedToolchain.toolchain.Path}");
            }
            else
            {
                Console.WriteLine($"Project is version {unityProject.GetVersion()}");
                Console.WriteLine($"Found compatible version {detectedToolchain.version} (from {UnityConstants.EditorsYmlFileName}) in {detectedToolchain.toolchain.Path}");
                Console.Error.WriteLine("Only working with exact matches for now (WIP!)");
                return CliExitCode.ErrorUnavailable;
            }

            useToolchain = detectedToolchain.toolchain;
        }

        // build up cli and environment

        var unityArgs = new List<string> { "-projectPath", unityProject.Path };
        var unityEnv = new Dictionary<string, object>{ {"UNITY_MIXED_CALLSTACK", 1}, {"UNITY_EXT_LOGGING", 1} }; // alternative to UNITY_EXT_LOGGING: "-timestamps" on command line

        if (context.GetConfigBool("enable-debugger"))
        {
            //TODO status "Enables debug code optimization mode, overriding the current default code optimization mode for the session."
            unityArgs.Add("-debugCodeOptimization");
        }

        if (context.GetConfigBool("wait-attach-debugger"))
        {
            //TODO status "Will wait for debugger on Unity startup"
            unityEnv.Add("UNITY_GIVE_CHANCE_TO_ATTACH_DEBUGGER", 1);
        }

        if (context.GetConfigBool("enable-coverage"))
        {
            //TODO status "Enables code coverage and allows access to the Coverage API."
            unityArgs.Add("-enableCodeCoverage");
        }

        if (context.TryGetConfigString("stack-trace-log", out var stackTraceLogType))
        {
            unityArgs.Add("-stackTraceLogType");

            // unity is case-sensitive and does not validate the arg, so make it nicer here
            unityArgs.Add(stackTraceLogType.ToLower() switch
            {
                "none" => "None",
                "scriptonly" => "ScriptOnly",
                "full" => "Full",
                string bad => throw new DocoptInputErrorException($"Illegal stack-trace-log type {bad}")
            });
        }

        if (context.GetConfigBool("rider"))
        {
            //TODO status "Opening Rider on {slnname} after project open in Unity"
            unityArgs.Add("-executeMethod");
            unityArgs.Add("Packages.Rider.Editor.RiderScriptEditor.SyncSolutionAndOpenExternalEditor");
        }

        if (context.GetConfigBool("no-burst"))
        {
            //TODO status "Disabling Burst"
            unityArgs.Add("--burst-disable-compilation");
        }

        if (context.GetConfigBool("verbose-upm-logs"))
        {
            //TODO status "Turning on extra debug logging for UPM ({UnityConstants.UpmLogPath.ToNPath().ToNiceString()})"
            unityArgs.Add("-enablePackageManagerTraces");
        }

        if (context.GetConfigBool("no-local-log"))
        {
            //TODO status "Debug logging to default ({UnityConstants.UnityEditorDefaultLogPath.ToNPath().ToNiceString()})"
        }
        else
        {
            //TODO status "Debug logging to %path"
            //TODO make log pathspec configurable

            var logPath = unityProject.Path.ToNPath()
                .Combine(UnityProjectConstants.LogsPath)
                .EnsureDirectoryExists()
                .Combine($"{unityProject.Name}-editor.log");

            // rotate old log
            // TODO: give option to cap by size and/or file count the logs
            // TODO: give format config for rotation name
            if (logPath.FileExists())
            {
                var targetPath = logPath.ChangeFilename($"{unityProject.Name}-editor_{logPath.FileInfo.CreationTime:yyyyMMdd_HHMMss}.log");
                if (doit)
                {
                    // TODO: make all of this a utility function, and add file-exists exception safeties

                    // never want a chance to lose log data
                    if (targetPath.FileExists())
                    {
                        var bakPath = targetPath.ChangeExtension(targetPath.ExtensionWithoutDot + ".bak");
                        var serial = 1;
                        while (bakPath.FileExists())
                            bakPath = targetPath.ChangeExtension(targetPath.ExtensionWithoutDot + ".bak" + serial++);
                        File.Move(targetPath, bakPath);
                    }
                    File.Move(logPath, targetPath);

                    // create and delete the logfile to work around "tunneling" feature and guarantee that the new file created gets current timestamp
                    // (see https://web.archive.org/web/20150307194932/http://support.microsoft.com/kb/172190)
                    logPath.CreateFile();
                    File.SetCreationTime(logPath, DateTime.Now);
                    logPath.Delete();
                }
                else
                    Console.WriteLine($"[dryrun] Rotating previous log file {logPath.RelativeTo(unityProject.Path)} to {targetPath.RelativeTo(unityProject.Path)}");
            }

            unityArgs.Add("-logFile");
            unityArgs.Add(logPath);
        }

        var sceneName = context.GetConfigString("scene");
        NPath? scenePath = null;
        if (sceneName != null)
        {
            scenePath = sceneName.ToNPath();
            if (scenePath.ExtensionWithDot != UnityProjectConstants.SceneFileExtension)
                scenePath = scenePath.ChangeExtension(UnityProjectConstants.SceneFileExtension);

            // TODO: build and leverage some kind of scene finder utility
            if (scenePath.IsRelative)
            {
                foreach (var basePath in new[]
                         {
                             NPath.CurrentDirectory,
                             unityProject.Path.ToNPath(),
                             unityProject.Path.ToNPath().Combine(UnityProjectConstants.AssetsPath)
                         })
                {
                    var testPath = basePath.Combine(scenePath);
                    if (testPath.FileExists())
                    {
                        scenePath = testPath;
                        break;
                    }
                }
            }

            if (!scenePath.FileExists())
                throw new DocoptInputErrorException($"Scene '{sceneName}' was not found in project");

            if (!scenePath.IsChildOf(unityProject.Path))
                throw new DocoptInputErrorException($"Scene '{scenePath}' is not part of project at '{unityProject.Path}'");

            scenePath = scenePath.RelativeTo(unityProject.Path);
        }

        if (context.Config.TryGetString("unity", null, "extra-args", out var extraArgs))
            unityArgs.AddRange(CliUtility.ParseCommandLineArgs(extraArgs));
        unityArgs.AddRange(context.CommandLine["EXTRA"].AsStrings());

        // TODO: -vvv style verbose level logging support

        if (doit)
        {
            if (scenePath != null)
            {
                var setupPath = unityProject.Path.ToNPath().Combine(UnityProjectConstants.LastSceneManagerSetupPath);
                setupPath.EnsureParentDirectoryExists();
                setupPath.WriteAllText("sceneSetups:\n- path: " + scenePath);
            }

            var unityStartInfo = new ProcessStartInfo
            {
                FileName = useToolchain.EditorExePath,
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
            Console.WriteLine("[dryrun]   path        | " + useToolchain.EditorExePath);
            Console.WriteLine("[dryrun]   arguments   | " + CliUtility.CommandLineArgsToString(unityArgs));
            Console.WriteLine("[dryrun]   environment | " + unityEnv.Select(kvp => $"{kvp.Key}={kvp.Value}").StringJoin("; "));
            if (scenePath != null)
                Console.WriteLine("[dryrun]   scene       | " + scenePath);
        }

        return CliExitCode.Success;
    }

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    static CliExitCode? TryActivateExistingUnity(UnityProject project, bool activateMainWindow)
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

            if (activateMainWindow)
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
