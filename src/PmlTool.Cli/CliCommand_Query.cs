using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NiceIO;
using OkTools.ProcMonUtils;

static partial class Program
{
    static CliExitCode Query(PmlToolCliArguments opts)
    {
        var pmlPath = opts.ArgPml!.ToNPath();
        if (!pmlPath.HasExtension())
            pmlPath = pmlPath.ChangeExtension(".pml");

        using var pmlReader = new PmlReader(pmlPath);

        var pmlBakedPath = pmlPath.ChangeExtension(".pmlbaked").FileMustExist();

        SymbolicatedEventsDb? symbolicatedEventsDb = null;
        void EnsureHaveEventsDb()
        {
            if (symbolicatedEventsDb != null)
                return;

            Console.Write("Loading symbolicated events db...");
            symbolicatedEventsDb = new SymbolicatedEventsDb(pmlBakedPath);
            Console.Write("\r                                 \r");
        }

        void Dump(uint pmlEventIndex)
        {
            var pmlEvent = pmlReader.GetEvent((int)pmlEventIndex);

            Console.WriteLine();
            Console.WriteLine($"[{pmlEvent.EventIndex}]");
            Console.WriteLine("  CaptureTime = " + pmlEvent.CaptureDateTime.ToString(PmlUtils.CaptureTimeFormat));
            var process = pmlReader.ResolveProcess(pmlEvent.ProcessIndex);
            Console.WriteLine($"  Process = {process.ProcessId} ({process.ProcessName})");
            Console.WriteLine($"  Thread ID = {pmlEvent.ThreadId}");

            if (pmlEvent.Frames?.Length > 0)
            {
                EnsureHaveEventsDb();
                var eventRecord = symbolicatedEventsDb!.GetRecord(pmlEventIndex);
                if (eventRecord != null)
                {
                    Console.WriteLine("  Frames:");

                    var sb = new StringBuilder();

                    for (var i = 0; i < eventRecord.Value.Frames.Length; ++i)
                    {
                        ref var frame = ref eventRecord.Value.Frames[i];
                        sb.Append($"    {eventRecord.Value.Frames.Length-i-1:00} {frame.Type.ToString()[0]}");
                        if (frame.ModuleStringIndex != 0)
                            sb.Append($" [{symbolicatedEventsDb.GetString(frame.ModuleStringIndex)}]");

                        sb.Append(frame.SymbolStringIndex != 0
                            ? $" {symbolicatedEventsDb.GetString(frame.SymbolStringIndex)} + 0x{frame.Offset:x}"
                            : $" 0x{frame.Offset:x}");

                        Console.WriteLine(sb);
                        sb.Clear();
                    }
                }
                else
                    Console.WriteLine("  Frames: <failed pmlbaked lookup>");
            }
            else
                Console.WriteLine("  Frames: <none>");
        }

        foreach (var query in opts.ArgQuery)
        {
            if (uint.TryParse(query, out var eventIdArg))
            {
                Dump(eventIdArg);
            }
            else if (query.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && uint.TryParse(query.AsSpan(2), NumberStyles.HexNumber, null, out eventIdArg))
            {
                Dump(eventIdArg);
            }
            else if (DateTime.TryParse(query, out var captureTime))
            {
                // just have to seek for it, no easy way to find
                var found = false;
                foreach (var pmlEvent in pmlReader.SelectEvents())
                {
                    if (pmlEvent.CaptureDateTime == captureTime)
                    {
                        Dump(pmlEvent.EventIndex);
                        found = true;
                        break;
                    }
                }

                if (!found)
                    Console.Error.WriteLine("No event found matching " + captureTime.ToString(PmlUtils.CaptureTimeFormat));
            }
            else
            {
                EnsureHaveEventsDb();

                var regex = new Regex(query);
                var eventIds = Enumerable.Concat(
                    symbolicatedEventsDb!.MatchRecordsByModule(regex),
                    symbolicatedEventsDb!.MatchRecordsBySymbol(regex));

                var found = false;
                foreach (var eventId in eventIds)
                {
                    Dump(eventId);
                    found = true;
                }

                if (!found)
                    Console.Error.WriteLine($"No events found matching module/symbol regex '{query}'");
            }
        }

        return CliExitCode.Success;
    }
}
