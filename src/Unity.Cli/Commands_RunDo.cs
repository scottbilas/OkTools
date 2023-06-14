using System.Globalization;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
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
  okunity do monosym [--pid PID] --live
{DocUsageDoUsage_Windows}
Commands:
  monosym  Look up mono-jit symbols for the given process, then modify the stack in FILE to have
           symbolicated symbols where matches are found. If no FILE is given, then the clipboard
           will be used instead.
{DocUsageDoCommands_Windows}
Options:
  --pid PID   The process ID to use when finding the correct mono pmip file with symbols. If not
              given, then it will use the most recently modified pmip_*.txt file from %TEMP%.
  --live      Will monitor the clipboard and print symbolicated stacks to stdout when it contains
              changed text that looks like a callstack.
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

    // TODO move this out to a utility near MonoJitSymbolDb
    static MonoJitSymbolDb CreateSymbolDb(int pid = 0)
    {
        var monoPmipPattern = "pmip_*.txt";
        if (pid != 0)
            monoPmipPattern = $"pmip_{pid}_*.txt";

        // find the pmip file we want
        var monoPmipPath = NPath.SystemTempDirectory.Files(monoPmipPattern).MaxBy(f => f.FileInfo.LastWriteTime);
        if (monoPmipPath != null)
            return new MonoJitSymbolDb(monoPmipPath);

        string error;

        if (pid != 0)
            error = $"No pmip files found for process {pid} in %TEMP%. Possible causes of failure:\n" +
                    $"  - {pid} is not a Unity process or is not running\n";
        else
            error = "No pmip files found in %TEMP%. Possible causes of failure:\n";

        error += "  - The env var UNITY_MIXED_CALLSTACK=1 is not set\n" +
                 "  - Unity was either not started, or it is running but Mono has not started yet\n" +
                 "  - Unity shut down or died, and the pmip file has been auto-deleted on process exit\n";

        throw new CliErrorException(CliExitCode.ErrorNoInput, error);
    }

    public static CliExitCode RunDoMonoSym(CommandContext context)
    {
        var optPid = context.CommandLine["--pid"].AsInt;
        var optFile = context.CommandLine["FILE"].ToString();
        var displayFile = optFile == "" ? "clipboard" : optFile;
        var rx = new Regex(@"<unknown> 0x([0-9a-zA-Z]+)\b");

        var live = context.CommandLine["--live"].IsTrue;
        if (live)
        {
            Console.WriteLine("Monitoring clipboard for callstacks...");
            Console.WriteLine("--------------------------------------");
        }

        var lastStack = "";

        for (;;)
        {
            // get the stack
            var stack = optFile == ""
                ? ClipboardService.GetText() ?? ""
                : File.ReadAllText(optFile); // TODO: if want to support --live on FILE, then do a check for timestamp instead of re-reading it (for now just keeping code simple)

            // doesn't look like a stack?
            if (!rx.IsMatch(stack))
            {
                if (!live)
                    throw new CliErrorException(CliExitCode.ErrorNoInput, $"No symbols to match in {displayFile}");
            }
            else if (stack != lastStack)
            {
                lastStack = stack;

                // TODO: make this incremental, so we can just monitor for added lines and insert them in the db
                var symbolDb = CreateSymbolDb(optPid);

                var replaced = 0;
                var symbolicated = rx.Replace(stack, match =>
                {
                    var addr = ulong.Parse(match.Groups[1].Value, NumberStyles.HexNumber);
                    if (!symbolDb.TryFindSymbol(addr, out var symbol))
                        return match.Value;

                    ++replaced;
                    return symbol.ToString(addr);
                });

                if (replaced == 0)
                    Console.WriteLine($"No symbols matched in {displayFile}");
                else
                    Console.WriteLine($"{replaced} symbols matched and replaced in {displayFile}");

                if (live)
                {
                    const string k_Prefix = "  ... ";
                    Console.WriteLine("-----");

                    foreach (var line in symbolicated.Split('\n'))
                    {
                        var display = (line.StartsWith('[') ? "> " : "  ") + line.Trim();

                        var wrapping = false;
                        for (;;)
                        {
                            var len = Math.Min(display.Length, Console.WindowWidth - 1 - (wrapping ? k_Prefix.Length : 0));
                            if (len == 0)
                                break;

                            var print = display[..len];
                            if (wrapping)
                                print = k_Prefix + print.TrimStart();

                            Console.WriteLine(print);
                            display = display[len..];
                            wrapping = true;
                        }
                    }
                    Console.WriteLine("-----");

                    Console.WriteLine();
                }
                else if (optFile != "")
                    File.WriteAllText(optFile, symbolicated);
                else
                    ClipboardService.SetText(symbolicated);
            }

            if (!live)
                break;

            Thread.Sleep(250);
        }

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
