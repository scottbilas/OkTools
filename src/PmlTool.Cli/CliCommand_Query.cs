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
        var baseTime = pmlReader.GetEvent(0).CaptureDateTime;

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

        void Dump(int pmlEventIndex)
        {
            var pmlEvent = pmlReader.GetEvent(pmlEventIndex);

            Console.WriteLine();
            Console.WriteLine($"[{pmlEvent.EventIndex}]");
            Console.WriteLine($"  CaptureTime = {pmlEvent.CaptureDateTime.ToString(PmlUtils.CaptureTimeFormat)} (abs: {(pmlEvent.CaptureDateTime - baseTime).ToString()})");
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
                            ? $" {symbolicatedEventsDb.GetString(frame.SymbolStringIndex)} + 0x{frame.AddressOrOffset:x}"
                            : $" 0x{frame.AddressOrOffset:x}");

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

        int? RunQuery(string query, out bool parsed)
        {
            parsed = true;

            if (int.TryParse(query, out var queryEventId))
                return queryEventId;
            if (query.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(query.AsSpan(2), NumberStyles.HexNumber, null, out queryEventId))
                return queryEventId;

            var timeMatch = Regex.Match(query, @"(\d\d)[.:](\d\d)[.:](\d\d)[,.](\d{7})");
            if (timeMatch.Success)
            {
                var (h, m, s, ms) = (
                    int.Parse(timeMatch.Groups[1].Value),
                    int.Parse(timeMatch.Groups[2].Value),
                    int.Parse(timeMatch.Groups[3].Value),
                    int.Parse(timeMatch.Groups[4].Value));
                var queryFileTime = ((h*60+m)*60+s) * 10_000_000L + ms; // 7 digits has ms == 100ns units

                // just have to seek for it, no easy way to find
                foreach (var pmlEvent in pmlReader.SelectEvents())
                {
                    var eventTime = pmlEvent.CaptureDateTime;
                    var eventFileTime = eventTime.ToFileTime() - new DateTime(eventTime.Year, eventTime.Month, eventTime.Day).ToFileTime();

                    if (queryFileTime == eventFileTime)
                        return pmlEvent.EventIndex;
                    if (queryFileTime < eventFileTime)
                        return null;
                }
            }
            else if (DateTime.TryParse(query, out var queryTime))
            {
                var queryFileTime = queryTime.ToFileTime();

                // just have to seek for it, no easy way to find
                foreach (var pmlEvent in pmlReader.SelectEvents())
                {
                    if (queryFileTime == pmlEvent.CaptureTime)
                        return pmlEvent.EventIndex;
                    if (queryFileTime < pmlEvent.CaptureTime)
                        break;
                }
            }
            else
                parsed = false;

            return null;
        }

        foreach (var query in opts.ArgQuery)
        {
            var parts = query.Split("..", 2);
            if (parts.Length == 1)
            {
                var startEnd = RunQuery(parts[0], out var parsed);
                if (parsed)
                {
                    if (startEnd != null)
                        Dump(startEnd.Value);
                    else
                        Console.Error.WriteLine($"No events found for given query '{parts[0]}'");
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
            else
            {
                var start = RunQuery(parts[0], out var parsed);
                if (!parsed)
                    Console.Error.WriteLine($"Unable to parse query '{parts[0]}'");
                else if (start == null)
                    Console.Error.WriteLine($"No events found for given query '{parts[0]}'");

                var end = RunQuery(parts[1], out parsed);
                if (!parsed)
                    Console.Error.WriteLine($"Unable to parse query '{parts[1]}'");
                else if (end == null)
                    Console.Error.WriteLine($"No events found for given query '{parts[1]}'");

                if (start != null && end != null)
                {
                    for (var i = start.Value; i <= end.Value; ++i)
                        Dump(i);
                }
            }
        }

        return CliExitCode.Success;
    }
}
