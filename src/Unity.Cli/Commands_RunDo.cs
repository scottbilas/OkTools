using System.Globalization;
using DocoptNet;
using NiceIO;
using OkTools.ProcMonUtils;
using TextCopy;

static partial class Commands
{
    public const string DocUsageDo =
@"Usage:
  okunity do monosym [--pid PID] [FILE]

Commands:
  monosym  Look up mono-jit symbols for the given process, then modify the stack in FILE to have
           symbolicated symbols where matches are found. If no FILE is given, then the clipboard
           will be used instead.

Options:
    --pid PID  The process ID to use when finding the correct mono pmip file with symbols. If not
               given, then it will use the most recently modified pmip_*.txt file from %TEMP%.
";

    public static CliExitCode RunDo(CommandContext context)
    {
        if (context.CommandLine["monosym"].IsTrue)
            return RunDoMonoSym(context);

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
            Console.WriteLine("No unknown symbols matched in {displayFile}");
            return CliExitCode.Success;
        }

        Console.WriteLine($"{replaced} symbols matched and replaced in {displayFile}");

        if (optFile == "")
            ClipboardService.SetText(symbolicated);
        else
            File.WriteAllText(optFile, symbolicated);

        return CliExitCode.Success;
    }
}
