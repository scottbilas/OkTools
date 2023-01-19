using System.Globalization;
using System.Runtime.Versioning;
using DocoptNet;
using NiceIO;
using OkTools.ProcMonUtils;
using OkTools.Unity;
using TextCopy;

static partial class Commands
{
    // TODO: not loving this constructive platform-specific help stuff..

    public static readonly string DocUsageDo =
@$"Usage:
  okunity do monosym [--pid PID] [FILE]
{DocUsageDoUsage_Windows}
Commands:
  monosym  Look up mono-jit symbols for the given process, then modify the stack in FILE to have
           symbolicated symbols where matches are found. If no FILE is given, then the clipboard
           will be used instead.
{DocUsageDoCommands_Windows}
Options:
  --pid PID   The process ID to use when finding the correct mono pmip file with symbols. If not
              given, then it will use the most recently modified pmip_*.txt file from %TEMP%.
  --dry-run   Don't change anything, just print what would happen.
{DocUsageDoOptions_Windows}
";

    public static string DocUsageDoUsage_Windows => !OperatingSystem.IsWindows() ? "" :
@"  okunity do killhub [--dry-run]
  okunity do hidehub [--kill-hub] [--dry-run]
";

    public static string DocUsageDoCommands_Windows => !OperatingSystem.IsWindows() ? "" : @"
  killhub  Kill any Unity Hub processes that are running.

  hidehub  Hack the various places required to prevent Unity from auto-launching the Hub:

           1. `%AppData%\UnityHub\hubInfo.json`

              This is where Unity first looks to find the Hub exe. We rename it to hubInfo.json.bak
              (overwriting any file that already exists).

           2. `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Unity Technologies - Hub\UninstallString`

              Next, Unity will check this registry value to find the Hub exe. We rename it to
              UninstallString.bak (overwriting any value that already exists).

           Upon the next manual launch or update/reinstall of the Hub, either or both of these
           will be restored and you will have to run `do hidehub` again.

           Note: the `unity` command has a `--do-killhub` option that will automatically run
           `do killhub`, and can also be set to happen by default in the config file.
";

    public static string DocUsageDoOptions_Windows => !OperatingSystem.IsWindows() ? "" :
@"  --kill-hub  Also run `do killhub` as part of this command.
";

    public static CliExitCode RunDo(CommandContext context)
    {
        if (context.CommandLine["monosym"].IsTrue)
            return RunDoMonoSym(context);

        if (OperatingSystem.IsWindows() && context.CommandLine["killhub"].IsTrue)
        {
            KillHub(context.CommandLine["--dry-run"].IsTrue);
            return CliExitCode.Success;
        }

        if (OperatingSystem.IsWindows() && context.CommandLine["hidehub"].IsTrue)
        {
            HideHubFromUnity(context.CommandLine["--kill-hub"].IsTrue, context.CommandLine["--dry-run"].IsTrue);
            return CliExitCode.Success;
        }

        throw new DocoptInputErrorException();
    }

    public static CliExitCode RunDoMonoSym(CommandContext context)
    {
        var monoPmipPattern = "pmip_*.txt";
        var optPid = context.CommandLine["--pid"].AsInt;
        if (optPid != 0)
            monoPmipPattern = $"pmip_{optPid}_*.txt";

        // find the pmip file we want
        var monoPmipPath = NPath.SystemTempDirectory.Files(monoPmipPattern).MaxBy(f => f.FileInfo.LastWriteTime);
        if (monoPmipPath == null)
        {
            if (optPid != 0)
            {
                Console.Error.WriteLine($"No pmip files found for process {optPid} in %TEMP%. Possible causes of failure:");
                Console.Error.WriteLine($"  - {optPid} is not a Unity process or is not running");
            }
            else
                Console.Error.WriteLine("No pmip files found in %TEMP%. Possible causes of failure:");

            Console.Error.WriteLine("  - The env var UNITY_MIXED_CALLSTACK=1 is not set");
            Console.Error.WriteLine("  - Unity was either not started, or it is running but Mono has not started yet");
            Console.Error.WriteLine("  - Unity shut down or died, and the pmip file has been auto-deleted on process exit");
            return CliExitCode.ErrorNoInput;
        }

        // parse it into a db for symbolicating our stack
        var symbolDb = new MonoJitSymbolDb(monoPmipPath);

        // get the stack
        var optFile = context.CommandLine["FILE"].ToString();
        var displayFile = optFile == "" ? "clipboard" : optFile;
        var stack = optFile == ""
            ? ClipboardService.GetText() ?? ""
            : File.ReadAllText(optFile);

        var (matched, replaced) = (0, 0);
        var symbolicated = stack.RegexReplace(@"<unknown> 0x([0-9a-zA-Z]+)\b", match =>
        {
            ++matched;

            var addr = ulong.Parse(match.Groups[1].Value, NumberStyles.HexNumber);
            if (!symbolDb.TryFindSymbol(addr, out var symbol))
                return match.Value;

            ++replaced;
            return symbol.ToString(addr);
        });

        if (matched == 0)
        {
            Console.WriteLine($"No unknown symbols to match in {displayFile}");
            return CliExitCode.Success;
        }

        if (replaced == 0)
        {
            Console.WriteLine($"No unknown symbols matched in {displayFile}");
            return CliExitCode.Success;
        }

        Console.WriteLine($"{replaced} symbols matched and replaced in {displayFile}");

        if (optFile == "")
            ClipboardService.SetText(symbolicated);
        else
            File.WriteAllText(optFile, symbolicated);

        return CliExitCode.Success;
    }

    [SupportedOSPlatform("windows")]
    static void KillHub(bool dryRun)
    {
        var killed = UnityHub.KillHubIfRunning(dryRun);
        if (killed > 0)
            StatusLine(dryRun, $"Killed {killed} Unity Hub processes");
        else
            Console.WriteLine("No Unity Hub processes found to kill");
    }

    [SupportedOSPlatform("windows")]
    static void HideHubFromUnity(bool killHubAlso, bool dryRun)
    {
        if (killHubAlso)
            KillHub(dryRun);

        var state = UnityHub.HideHubFromUnity(dryRun);
        if (state == UnityHub.HubHiddenState.NeedSudo) // TODO: if interactive (and with an opt-out command line flag..), offer to sub-launch elevated `do killhub` with inherited stdout/err
            Console.Error.WriteLine("Sudo required to fully hide Unity Hub from Unity");
        else
            StatusLine(dryRun, $"Hiding Unity Hub from Unity ({state})");
    }
}
