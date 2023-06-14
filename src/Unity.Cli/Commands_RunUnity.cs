using System.Diagnostics;
using System.Runtime.InteropServices;
using DocoptNet;
using NiceIO;
using OkTools.ProcMonUtils;
using OkTools.Unity;

static partial class Commands
{
    // TODO: partial matches (like ignore hash and go with most recent install)
    // TODO: option to kill unity if found running (perhaps only if not responding..otherwise give it a quit message and a 15s or whatever timeout, then fall back to kill)
    // TODO: --create-project option (probably should also require --toolchain..difficult to choose a default)
    // TODO: --kill-hub option
    // TODO: let --toolchain specify another project path or a projectversion so can mean "launch project x with the same unity as project y uses"
    // TODO: `--prefer-toolchain LocallyBuilt` or something that lets me say "use the one i built rather than the installed" and does a semi-fuzzy match on versioning, because obvs it will be a bit different from projectversion.txt expectation
    // TODO: use '!' to mean "last". for example `oku unity !` will run unity with the last project chosen. (requires obvs saving out this data..also need to be careful with context about where we might allow a ! for any given flag..what about --toolchain !, right? last for that project, last for any project..? use !! for 'global' and ! for "last local if applicable"?)
    // TODO: add a --buildTarget that will also optionally do a Library folder swaperoo (maybe..)
    // TODO: add a --buildtype that will do nice things like check in advance for NINTENDO_SDK_ROOT set and exists if running with Switch type..
    // TODO: add a --template to be used with --create-project; see UnityCapture.ps1 for how to find the local templates

/*
    * Choose an EXE
        * Match something I already have on my machine (latest beta or released, or give me a chooser)
        * Warnings about debug vs release, or mismatched project causing an upgrade (does Unity do this already? or was it the Hub?)
    * Set env and command line etc.
        * Make Unity's flags visible with tab completion (something like Crescendo would do..except Crescendo doesn't seem to support parameterized exe location..)
        * Support an individual project's extensions to tab completion for any command line flags the project supports
        * PSReadLine support for tabbing through matching discovered unity versions
    * Support the exe run
        * Hub killing
        * Log rotation
        * Automatic bringing to front of an existing Unity (avoid Unity's stupid handling of this)
*/

    // TODO: mark these up for where they come from command line OR config file..(not all of them do yet, until lame .netconfig is fixed)

    // TODO: have a `okunity help unity-cli` that will download `https://docs.unity3d.com/Manual/EditorCommandLineArguments.html` and do a little bit of parsing to extract the args into a grid

    public static readonly string DocUsageUnity =
@$"Usage: okunity unity [options] [PROJECT] [-- EXTRA...]

Description:
  Run Unity to open the given PROJECT (defaults to '.'). Uses the project version given to find the matching
  Unity toolchain; see `okunity help toolchains` for how toolchains are discovered.

  Any arguments given in EXTRA will be passed along to the newly launched Unity process. Unity doesn't have a way to
  discover all possible cli args for the editor or any game projects - anybody can process argv/argc - but here are
  some good places to look:

  * See official docs at https://docs.unity3d.com/Manual/EditorCommandLineArguments.html
  * Grep Unity source for `BootConfig::Parameter`
  * Grep Unity source for `HasARGV`

  All of the below options will only apply to the new Unity session being launched.

Options:
  --dry-run               Don't change anything, only print out what would happen instead
  --create-project        Create a new project at PROJECT, which must be a folder that does not exist
  --toolchain TOOLCHAIN   Ignore project version and use this toolchain (can be full/partial version, path to toolchain, or unityhub link)
  --scene SCENE           After loading the project, also load this specific scene (creates or overwrites {UnityProjectConstants.LastSceneManagerSetupPath.ToNPath().ToNiceString()})
  --gen-project-only      Generate the project files and then exit
  --ide                   Open code IDE after the project opens in Unity
  --wait-unity            Wait for the Unity process to terminate, then return its exit code (*)
  --unity-process-stats   Print out stats for the Unity process (implies `--wait-unity`)
  --copy-pmips PMIPS      Watch for pmip_*.txt files created by Mono and copy them, preserving create-time, to the PMIPS folder on Unity exit (implies `--wait-unity`)
  --enable-coverage       Enable Unity code coverage
  --pid-exitcode          Return the Unity process ID as the exit code (*)
  --job-worker-count JWC  Set a limit on both a) job worker thread count and b) shader compiler process count; JWC can be either X to choose an explicit number or X% to choose a percentage of machine vCPU's
  --no-cache-server       Tell Unity not to use the cache server
  --no-hub                windows-only: Run `okunity do hidehub --kill-hub` before launching Unity, which will kill the Hub if running and also prevent the auto-launch of the Hub that Unity does (note that this change has global impact, check `help do` for more info on this)
  --no-burst              Completely disable Burst
  --no-activate-existing  Don't activate an existing Unity main window if found running on the project

  (*) The high bit will also be set to differentiate from normal CLI exit codes.

Log Options:
  --no-local-log          Disable local log feature
  --no-log-rotation       Disable the log rotation feature
  --log-file LOGFILE      Use this log file instead of the default one
  --stack-trace-log TYPE  Override Unity settings to use the given stack trace level for logs (TYPE can be None, ScriptOnly, or Full)
  --verbose-upm-logs      Tell Unity Package Manager to write verbose logs ({UnityConstants.UpmLogPath.ToNPath().ToNiceString()})

  Unity is always directed to include timestamps in its log files, which will have this format:
    <year>-<month>-<day>T<hour>:<minute>:<second>.<milliseconds>Z|<thread-id>|<log-line>
  The timestamp is unfortunately recorded in unadjusted UTC.

  The Unity log file will be stored at:

  * (default)      -> use `Logs/<ProjectName>-editor.log`
  * --log-file     -> use the given path LOGFILE (overrides `--no-local-log`)
  * --no-local-log -> use `{UnityConstants.UnityEditorDefaultLogPath.ToNPath().ToNiceString()}` (Unity's default behavior)

  Unless `--no-log-rotation` is set, any existing file with this name will be rotated out to a filename with a timestamp
  appended to it, thus preserving logs from previous sessions.

Debugging Options:
  --enable-debugging      Enable managed code debugging. This slows down Mono jitted code performance, so Unity does not
                          enable it by default, but it is required to do any debugging of script code.
  --wait-attach-managed   Unity will pause right after scripting system startup for attaching a managed debugger (implies `--enable-debugging`)
  --wait-attach-native    Unity will pause very early in startup so you can attach a native debugger (*)

  If using one of the `--wait-attach-*` options, Unity will pause during startup, show you a message box telling you
  to attach a debugger, and will continue execution upon clicking OK (regardless of whether you actually attached a
  debugger). This is very useful for when you want to debug startup code, such as static constructors and
  `[InitializeOnLoadMethod]`.

  (*) If you want to debug as early as possible, use the `--dry-run` option to get the command line flags okunity would
  normally pass to Unity, and copy those to your native debugger's startup settings.
";

    public static CliExitCode RunUnity(CommandContext context)
    {
        var pmipDstDir = context.GetConfigPath("copy-pmips");
        if (pmipDstDir?.DirectoryExists() == false)
        {
            Console.Error.WriteLine($"The --copy-pmips path '{pmipDstDir}' does not exist");
            return CliExitCode.ErrorUsage;
        }

        var doit = !context.CommandLine["--dry-run"].IsTrue;
        if (!doit)
            Console.WriteLine("[dryrun=true]");

        // TODO: move this into OkTools.Unity once we get a decent (typesafe) config-overlay system. that way, the CLI
        // can just overlay the config with whatever cl options it wants to support and keep all the logic in the lib.
        // posh cmdlet can do exact same thing, with no serious duplicated logic.
        //
        // idea: major util funcs in OkTools.Unity (like RunUnity) would receive an explicit config type. caller has to
        // produce this. cli and posh cmdlet args would be manually converted, but need some kind of DotNetConfig
        // defaults-provider support. maybe leave the dotnetconfig in the utility function. if the config types track
        // when they are overridden, then the dotnetconfig can be used as a default-provider "underlay".

        // get a valid unity project

        UnityProject? unityProject = null;
        NPath projectPath;
        var createProject = context.GetConfigBool("create-project");

        // scope
        {
            var projectConfig = context.CommandLine["PROJECT"].Value?.ToString();

            // special: docopt.net's processing of "--" is not greedy, so this situation can happen if EXTRA is passed but PROJECT is not (for example `unity --no-burst -- -extra-stuff`)
            if (context.CommandLine["--"].IsFalse && projectConfig == "--")
                 projectConfig = null;

            projectPath = new NPath(projectConfig ?? ".");
            if (createProject)
            {
                if (projectPath.DirectoryExists())
                {
                    Console.Error.WriteLine($"Can't create a new project in existing directory '{projectPath}'");
                    return CliExitCode.ErrorUsage;
                }
            }
            else
            {
                if (!projectPath.DirectoryExists())
                {
                    Console.Error.WriteLine($"Directory for Unity project does not exist '{projectPath}'");
                    return CliExitCode.ErrorNoInput;
                }

                var tryUnityProject = UnityProject.TryCreateFromProjectTree(projectPath);
                if (tryUnityProject == null)
                {
                    Console.Error.WriteLine($"Path is not in a Unity project '{projectPath}'");
                    return CliExitCode.ErrorNoInput;
                }

                unityProject = tryUnityProject;
                projectPath = unityProject.Path;

                // should never happen, unless i am debugging something in a project :)
                string? projectVersion = null;
                try
                {
                    projectVersion = unityProject.GetVersion().ToString();
                }
                catch (UnityVersionFormatException) { projectVersion = "<invalid format>"; }
                catch (FileNotFoundException) { /* this is ok, for test projects */ }

                if (projectVersion != null)
                    Console.Write($"Loading project at {unityProject.Path}; expects {projectVersion}");

                var lastOpened = unityProject.GetLastOpenedTime();
                if (lastOpened != null)
                    Console.Write($"; last opened {lastOpened.Value.ToNiceAge(true)}");
                Console.WriteLine();
            }

            projectPath = projectPath.MakeAbsolute();
        }

        if (unityProject != null)
        {
            // check if unity is already running on that project

            // TODO: warn if the unity running on it is a different version from detected (whether it's not part of GetTestableVersions(), or mismatches a direct --toolchain config)

            var existingUnityRc = TryActivateExistingUnity(unityProject, doit && !context.GetConfigBool("no-activate-existing"), context.GetConfigBool("pid-exitcode"));
            if (existingUnityRc != null)
            {
                // TODO: warn if the existing unity process received different command line flags or env than we were going to use here
                return existingUnityRc.Value;
            }
        }

        // find a matching toolchain

        UnityToolchain useToolchain;

        if (context.TryGetConfigString("toolchain", out var useToolchainConfig))
        {
            // given explicitly

            // TODO: explicit version may be fuzzy (like 2020.3). in that case, filter all found toolchains by that, then
            // pick whichever is the closest match to the project expected version (== is best, > is acceptable, < will require user being more explicit).
            // TODO: consider requiring a --downgrade-ok for even if the user is explicit about wanting an older version. can be very destructive, need to def be sure.
            // TODO: consider supporting a set of comma-delimited constraints, so i can say local;debug to have it find a locallybuild build with debug config that matches the version

            // try as path
            var testToolchain = UnityToolchain.TryCreateFromPath(useToolchainConfig);
            if (testToolchain == null)
            {
                var toolchains = FindAllToolchains(context.Config, false).MakeNice().Memoize();

                // try as version
                var testVersion = UnityVersion.TryFromText(useToolchainConfig);
                if (testVersion == null)
                {
                    // try as plain text match - could be a simple hash
                    // TODO: also try as UnityEditorBuildConfig and UnityToolchainOrigin (maybe comma-delimit as query operators...clearly need to make this a utility function..)
                    testToolchain = toolchains.FirstOrDefault(t => t.Version.Hash == useToolchainConfig);
                    if (testToolchain == null)
                    {
                        Console.Error.WriteLine($"TOOLCHAIN given ('{useToolchainConfig}') cannot be parsed to a valid toolchain path or Unity version");
                        return CliExitCode.ErrorUsage;
                    }
                }
                else
                {
                    testToolchain = FindAllToolchains(context.Config, false).MakeNice().FirstOrDefault(t => t.Version.IsMatch(testVersion));
                    if (testToolchain == null)
                    {
                        Console.Error.WriteLine($"Unable to find any toolchain with version {testVersion}");
                        return CliExitCode.ErrorUnavailable;
                    }
                }
            }

            useToolchain = testToolchain;
            Console.WriteLine($"Using toolchain with version {useToolchain.Version} at {useToolchain.Path}");
        }
        else if (unityProject != null)
        {
            // detect from project

            var testableVersions = unityProject.GetTestableVersions().Memoize();
            var foundToolchains = FindAllToolchains(context.Config, false).MakeNice().Memoize();

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
                Output(foundToolchains, 0, Console.Error);
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
        else
        {
            Debug.Assert(createProject);
            Console.Error.WriteLine("Must specify a toolchain when using --create-project");
            return CliExitCode.ErrorUsage;
        }

        // build up cli and environment

        // docs for unity's cl args: https://docs.unity3d.com/Manual/EditorCommandLineArguments.html

        // begin with the standard stuff i always want (TODO: consider moving these to config vars and set there instead..?)
        var unityArgs = new List<object>
        {
            createProject ? "-createProject" : "-projectPath", projectPath,
            "-monoProfiler", "superluminal", // on by default starting with 2021.3
        };
        var unityEnv = new Dictionary<string, object>
        {
            {"UNITY_MIXED_CALLSTACK", 1},
            {"UNITY_EXT_LOGGING", 1} // alternative to UNITY_EXT_LOGGING: "-timestamps" on command line
        };

        var enableManagedDebugging = context.GetConfigBool("enable-debugging");
        if (context.GetConfigBool("wait-attach-managed"))
        {
            //TODO status "Wait for managed debugger on Unity startup"
            unityArgs.Add("-wait-for-managed-debugger");
            enableManagedDebugging = true;
        }

        if (context.GetConfigBool("wait-attach-native"))
        {
            //TODO status "Wait for managed debugger on Unity startup"
            unityArgs.Add("-wait-for-native-debugger");
        }

        if (enableManagedDebugging)
        {
            //TODO status "Enables debug code optimization mode, overriding the current default code optimization mode for the session."
            unityArgs.Add("-debugCodeOptimization");
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
            unityArgs.Add(stackTraceLogType.ToLowerInvariant() switch
            {
                "none" => "None",
                "scriptonly" => "ScriptOnly",
                "full" => "Full",
                var bad => throw new DocoptInputErrorException($"Illegal stack-trace-log type {bad}")
            });
        }

        if (context.GetConfigBool("gen-project-only"))
        {
            unityArgs.Add("-batchmode");

            unityArgs.Add("-ignorecompilererrors");
            unityArgs.Add("-nographics");
            unityArgs.Add("-quit");
        }

        if (context.GetConfigBool("ide"))
        {
            // TODO: this doesn't get called if the editor is already running - instead, tell existing one to open c# project using socket

            //TODO status "Opening IDE on {slnname} after project open in Unity"

            // ideally we'd use `Unity.CodeEditor.CodeEditor.CurrentEditor.OpenProject` but -executeMethod requires
            // a static method, not something with properties it has to evaluate. so we use rider's nice function that
            // they were nice enough to add for me. note that even though it's in the rider package, it will run
            // whatever external editor is selected in prefs. so if you want to use `--ide` on a VS-only project, you
            // must still have the rider package included in the manifest.

            // TODO: if --ide, ensure rider package in manifest.json (careful with combining with --create-project)

            unityArgs.Add("-executeMethod");
            unityArgs.Add("Packages.Rider.Editor.RiderScriptEditor.SyncSolutionAndOpenExternalEditor");
        }

        if (context.TryGetConfigString("job-worker-count", out var workerCountTxt))
        {
            int workerCount;

            if (workerCountTxt.EndsWith('%'))
            {
                if (!float.TryParse(workerCountTxt[..^1], out var percent))
                    throw new DocoptInputErrorException($"Not a valid number: {workerCountTxt[..^1]}");
                if (percent <= 0 || percent > 100)
                    throw new DocoptInputErrorException($"Job worker count percent must be > 0 and <= 100 (given {workerCountTxt})");

                workerCount = (int)Math.Ceiling(percent * Environment.ProcessorCount / 100);
            }
            else
            {
                if (!int.TryParse(workerCountTxt, out workerCount))
                    throw new DocoptInputErrorException($"Not a valid number: {workerCountTxt}");
                if (workerCount <= 0 || workerCount > Environment.ProcessorCount)
                    throw new DocoptInputErrorException($"Job worker count percent must be > 0 and <= {Environment.ProcessorCount} (given {workerCountTxt})");
            }

            //TODO status $"Limiting job worker count to {workerCount}"
            unityArgs.Add("-job-worker-count");
            unityArgs.Add(workerCount);
        }

        if (context.GetConfigBool("no-cache-server"))
        {
            //TODO status $"Limiting job worker count to {workerCount}"
            unityArgs.Add("-disable-cache-server");
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

        var useDefaultLog = false;
        var logPath = UnityConstants.UnityEditorDefaultLogPath.ToNPath();

        if (context.TryGetConfigString("log-file", out var logFile))
        {
            //TODO status "Debug logging to %path"

            logPath = logFile;
        }
        else if (!context.GetConfigBool("no-local-log"))
        {
            //TODO status "Debug logging to %path"
            //TODO make log pathspec configurable

            logPath = projectPath
                .Combine(UnityProjectConstants.LogsPath)
                .EnsureDirectoryExists()
                .Combine($"{projectPath.FileName}-Editor.log");
        }
        else
        {
            //TODO status "Debug logging to default ({logPath})"

            useDefaultLog = true;
        }

        logPath = logPath.MakeAbsolute();

        if (!context.GetConfigBool("no-log-rotation"))
        {
            // rotate old log
            // TODO: give option to cap by size and/or file count the logs
            // TODO: give format config for rotation name
            if (logPath.FileExists())
            {
                var targetPath = logPath.ChangeFilename($"{logPath.FileNameWithoutExtension}_{logPath.FileInfo.CreationTime:yyyyMMdd_HHMMss}.{logPath.ExtensionWithoutDot}");

                if (doit)
                {
                    // TODO: make all of this a utility function, and add file-exists exception safeties
                    // TODO: catch an IOException and re-throw it wrapped with a message that says what we're actually doing and what the involved filenames are (File.Move doesn't store any of this in the exception message)
                    // TODO: maybe we can also run `handle` on the file if the error was detected to be one of those "cannot access because used by another process" errors..

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
                {
                    var from = logPath.IsChildOf(projectPath) ? logPath.RelativeTo(projectPath) : logPath;
                    var to = targetPath.IsChildOf(projectPath) ? targetPath.RelativeTo(projectPath) : targetPath;
                    Console.WriteLine($"[dryrun] Rotating previous log file {from} to {to}");
                }
            }
        }

        if (!useDefaultLog)
        {
            unityArgs.Add("-logFile");
            unityArgs.Add(logPath);
        }

        var sceneName = context.GetConfigString("scene");
        string? scenePathArg = null;
        if (sceneName != null)
        {
            // TODO: what if --create-project set?

            var scenePath = sceneName.ToNPath();
            if (scenePath.ExtensionWithDot != UnityProjectConstants.SceneFileExtension)
                scenePath = scenePath.ChangeExtension(UnityProjectConstants.SceneFileExtension);

            // TODO: build and leverage some kind of scene finder utility
            // TODO: implement support for Packages

            // Packages are handled with virtual file system, so we can't validate scene paths here without a lot more
            // work on project+packages pathing support.
            if (!string.Equals(scenePath.Elements[0], "Packages", StringComparison.OrdinalIgnoreCase))
            {
                if (scenePath.IsRelative)
                {
                    foreach (var basePath in new[]
                             {
                                 NPath.CurrentDirectory,
                                 projectPath,
                                 projectPath.Combine(UnityProjectConstants.AssetsPath)
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

                if (!scenePath.IsChildOf(projectPath))
                    throw new DocoptInputErrorException($"Scene '{scenePath}' is not part of project at '{projectPath}'");

                scenePath = scenePath.RelativeTo(projectPath);
            }

            scenePathArg = scenePath.ToString(SlashMode.Forward); // unity always expects forward slash paths
        }

        // TODO: aggregate a total set from config + command line (not as override)
        if (context.Config.TryGetString("unity", null, "extra-args", out var extraArgs))
            unityArgs.AddRange(CliUtility.ParseCommandLineArgs(extraArgs));
        unityArgs.AddRange(context.CommandLine["EXTRA"].AsStrings());

        // TODO: -vvv style verbose level logging support

        if (OperatingSystem.IsWindows() && context.GetConfigBool("no-hub"))
            HideHubFromUnity(true, !doit);

        if (doit)
        {
            if (scenePathArg != null)
            {
                var setupPath = projectPath.Combine(UnityProjectConstants.LastSceneManagerSetupPath);
                setupPath.EnsureParentDirectoryExists();
                setupPath.WriteAllText("sceneSetups:\n- path: " + scenePathArg);
            }

            var unityStartInfo = new ProcessStartInfo { FileName = useToolchain.EditorExePath };
            if (unityProject != null)
                unityStartInfo.WorkingDirectory = unityProject.Path;

            foreach (var arg in unityArgs)
                unityStartInfo.ArgumentList.Add(arg.ToString()!);
            foreach (var (envName, envValue) in unityEnv)
                unityStartInfo.Environment[envName] = envValue.ToString();

            var unityProcess = Process.Start(unityStartInfo);
            if (unityProcess == null)
                throw new InvalidProgramException($"Unexpected failure to start process '{unityStartInfo.FileName}'");

            Console.WriteLine("Launched Unity as pid " + unityProcess.Id);

            var dumpProcessStats = context.GetConfigBool("unity-process-stats");
            var (peakPhysicalMB, peakVirtualMB, peakPagedMB) = (0L, 0L, 0L);

            void UpdateProcessStats()
            {
                unityProcess.Refresh();
                peakPhysicalMB = unityProcess.PeakWorkingSet64        / (1024 * 1024);
                peakVirtualMB  = unityProcess.PeakVirtualMemorySize64 / (1024 * 1024);
                peakPagedMB    = unityProcess.PeakPagedMemorySize64   / (1024 * 1024);
            }

            // TODO: consider making this automatic if running with -batchMode
            // (maybe just add a --batch-mode instead, that does both)
            if (context.GetConfigBool("wait-unity") || dumpProcessStats || pmipDstDir != null)
            {
                var now = DateTime.Now;

                if (pmipDstDir != null)
                {
                    Console.WriteLine("Monitoring for pmip_*.txt files and waiting for ...");

                    var srcDir = NPath.SystemTempDirectory;
                    var prefix = $"pmip_{unityProcess.Id}_*.txt";

                    var monitoring = new Dictionary<NPath, FileStream>();

                    while (!unityProcess.HasExited)
                    {
                        foreach (var srcPath in srcDir.Files(prefix))
                        {
                            if (monitoring.ContainsKey(srcPath))
                                continue;

                            // Unity relies on the OS for tempfile handle-closing to delete the file. if we also open a
                            // handle, it will hang around after Unity closes the handles or exits entirely.
                            var stream = MonoJitSymbolDb.OpenPmipFile(srcPath);
                            monitoring.Add(srcPath, stream);
                            Console.WriteLine($"...discovered {srcPath}");
                        }

                        if (dumpProcessStats)
                            UpdateProcessStats();

                        Thread.Sleep(5);
                    }

                    // copy pmips from temp dir to user-specified dst dir
                    foreach (var (srcPath, srcStream) in monitoring)
                    {
                        // reuse the srcStream for the copy (File.Copy will fail due to share permissions)
                        var dstPath = pmipDstDir.Combine(srcPath.FileName);
                        using (var dstStream = File.Create(dstPath))
                            srcStream.CopyTo(dstStream);

                        // preserve createtime. pmltool can use this to match stack frames to valid pmip times.
                        File.SetCreationTime(dstPath, File.GetCreationTime(srcPath));
                    }

                    // the OS can delete the source files now
                    foreach (var file in monitoring.Values)
                        file.Close();
                }
                else if (dumpProcessStats)
                {
                    Console.Write("Gathering stats and waiting for Unity process to terminate...");

                    while (!unityProcess.HasExited)
                    {
                        UpdateProcessStats();
                        Thread.Sleep(5);
                    }
                }
                else
                {
                    Console.Write("Waiting for Unity process to terminate...");
                    unityProcess.WaitForExit();
                }

                Console.WriteLine($"exited with code {unityProcess.ExitCode} after {(DateTime.Now - now).ToNiceAge()}");

                if (dumpProcessStats)
                {
                    Console.WriteLine();
                    Console.WriteLine("Process stats:");
                    Console.WriteLine($" - peak physical: {peakPhysicalMB} MB");
                    Console.WriteLine($" - peak virtual : {peakVirtualMB} MB");
                    Console.WriteLine($" - peak paged   : {peakPagedMB} MB");
                }

                return AsCliExitCode(context.GetConfigBool("pid-exitcode")
                    ? unityProcess.Id
                    : unityProcess.ExitCode);
            }

            if (context.GetConfigBool("pid-exitcode"))
                return AsCliExitCode(unityProcess.Id);
        }
        else
        {
            Console.WriteLine("[dryrun] Executing Unity with:");
            Console.WriteLine("[dryrun]   path        | " + useToolchain.EditorExePath);
            Console.WriteLine("[dryrun]   arguments   | " + CliUtility.CommandLineArgsToString(unityArgs.SelectToStrings()));
            Console.WriteLine("[dryrun]   environment | " + unityEnv.Select(kvp => $"{kvp.Key}={kvp.Value}").StringJoin("; "));
            if (scenePathArg != null)
                Console.WriteLine("[dryrun]   scene       | " + scenePathArg);
        }

        return CliExitCode.Success;
    }

    static CliExitCode AsCliExitCode(int exitCode) => (CliExitCode)(exitCode | 1<<31);

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(nint hWnd);

    static CliExitCode? TryActivateExistingUnity(UnityProject project, bool activateMainWindow, bool pidAsExitCode)
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

            if (activateMainWindow)
            {
                Console.WriteLine("Process is responding, activating and bringing to the front!");
                SetForegroundWindow(mainUnity.MainWindowHandle);
            }
            else
                Console.WriteLine("Process is responding");

            if (pidAsExitCode)
                return (CliExitCode)(mainUnity.Id | 1<<31);

            return pidAsExitCode ? AsCliExitCode(mainUnity.Id) : CliExitCode.Success;
        }
        finally
        {
            foreach (var unityProcess in unityProcesses)
                unityProcess.Dispose();
        }
    }
}
