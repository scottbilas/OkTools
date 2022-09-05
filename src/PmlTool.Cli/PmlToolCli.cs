using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NiceIO;
using OkTools.ProcMonUtils;

const string programVersion = "0.1";
const string ntSymbolPathName = "_NT_SYMBOL_PATH";

var (exitCode, cliOptions) = PmlToolCliArguments.CreateParser().Parse(args, programVersion, PmlToolCliArguments.Help, PmlToolCliArguments.Usage);
if (exitCode != null)
    return (int)exitCode.Value;

if (cliOptions.CmdBake)
{
    // FUTURE: this eats up a ton of native memory, probably from loading and using all the PDB's and never unloading them.
    // can probably set up some kind of LRU+pdbsize unload strategy to keep it manageable.

    var cancel = false;
    string? currentModule = null;

    // yuck, replace this with nice RX
    var monitor = new Thread(() =>
    {
        DateTime? lastModuleUpdateTime = DateTime.Now;
        string? lastModule = null;

        // ReSharper disable once AccessToModifiedClosure LoopVariableIsNeverChangedInsideLoop
        while (!cancel)
        {
            var localCurrentModule = currentModule;
            if (localCurrentModule != lastModule)
            {
                lastModuleUpdateTime = DateTime.Now;
                lastModule = localCurrentModule;
            }
            else if (localCurrentModule != null && lastModuleUpdateTime != null)
            {
                var now = DateTime.Now;
                if ((now - lastModuleUpdateTime.Value).TotalSeconds > 2)
                {
                    lastModuleUpdateTime = null;
                    Console.Write($"[loading {localCurrentModule}]");
                }
            }

            Thread.Sleep(50);
        }
    });
    monitor.Start();

    string? ntSymbolPath = null;
    if (cliOptions.OptNoNtsymbolpath)
    {
        ntSymbolPath = "";
    }
    else if (cliOptions.OptNoSymbolDownload)
    {
        var oldvar = Environment.GetEnvironmentVariable(ntSymbolPathName);
        if (oldvar != null)
        {
            var newvar = Regex.Replace(oldvar, @"\bSRV\*([^*]+)\*http[^;]+", "$1", RegexOptions.IgnoreCase);
            if (newvar != oldvar)
            {
                Console.WriteLine($"Replacing {ntSymbolPathName}: {oldvar} -> {newvar}");
                ntSymbolPath = newvar;
            }
        }
    }
    else if ((Environment.GetEnvironmentVariable(ntSymbolPathName)?.IndexOf("http") ?? -1) != -1)
        Console.WriteLine($"{ntSymbolPathName} appears to be set to use a symbol server, which may slow down processing greatly..");

    using var pmlReader = new PmlReader(cliOptions.ArgPml!.ToNPath());
    var bakedFile = pmlReader.PmlPath.ChangeExtension(".pmlbaked");

    var iter = 0;
    PmlUtils.Symbolicate(pmlReader, new SymbolicateOptions {
        DebugFormat = cliOptions.OptDebug,
        NtSymbolPath = ntSymbolPath,
        ModuleLoadProgress = name => currentModule = name,
        Progress = (_, total) =>
        {
            if (iter++ == 0)
                Console.Write($"Writing {total} events to {bakedFile.MakeAbsolute()}...");
            else if (iter % 10000 == 0) Console.Write(".");
        }});

    cancel = true;
    Console.WriteLine("done!");
}
else if (cliOptions.CmdResolve)
{
    if ((Environment.GetEnvironmentVariable(ntSymbolPathName)?.IndexOf("http") ?? -1) == -1)
        Console.WriteLine($"{ntSymbolPathName} appears to be not set to use a symbol server!");

    Console.Write("Scanning call stacks for modules...");

    var modulePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var iter = 0;
    using (var pmlReader = new PmlReader(cliOptions.ArgPml!.ToNPath()))
    {
        foreach (var pmlEvent in pmlReader.SelectEvents())
        {
            foreach (var address in pmlEvent.Frames!)
            {
                if (pmlReader.ResolveProcess(pmlEvent.ProcessIndex).TryFindModule(address, out var module))
                    modulePaths.Add(module.ImagePath);

                if (iter++ % 10000000 == 0)
                    Console.Write(".");
            }
        }
    }
    Console.WriteLine("done!");

    var dbghelp = new SimpleSymbolHandler();
    foreach (var (modulePath, index) in modulePaths.OrderBy(_ => _).Select((p, i) => (p, b: i)))
    {
        var start = DateTime.Now;
        Console.Write($"{index+1}/{modulePaths.Count} Loading symbols for {modulePath}...");
        try
        {
            dbghelp.LoadModule(modulePath);
            var delta = DateTime.Now - start;
            Console.WriteLine($"done! ({delta.TotalSeconds:0.00}s)");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"fail! {e.GetType().Name}: {e.Message}");
        }
    }
}
else if (cliOptions.CmdQuery)
{
    var pmlPath = cliOptions.ArgPml!.ToNPath();
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
        symbolicatedEventsDb = new SymbolicatedEventsDb(pmlBakedPath!);
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

    foreach (var query in cliOptions.ArgQuery)
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
}
else if (cliOptions.CmdConvert)
{
    var pmlPath = cliOptions.ArgPml!.ToNPath().FileMustExist();
    using var converted = File.CreateText((cliOptions.ArgConverted ?? pmlPath + ".json").ToNPath());

    converted.WriteLine('[');

    using var pmlReader = new PmlReader(pmlPath);
    ulong? baseTime = null;

    var seenProcessIds = new HashSet<uint>();

    var pairId = 1;
    foreach (var rwEvent in pmlReader.SelectEvents(PmlReader.Filter.FileSystem | PmlReader.Filter.Details).OfType<PmlFileSystemReadWriteEvent>().SkipWhile(e => e.EventIndex < 137900))
    {
        baseTime ??= rwEvent.CaptureTime / 10;

        var process = pmlReader.ResolveProcess(rwEvent.ProcessIndex);
        if (seenProcessIds.Add(process.ProcessId))
        {
            converted.WriteLine("{"+
                $"\"name\":\"process_name\",\"ph\":\"M\",\"pid\":{process.ProcessId},"+
                $"\"args\":{{\"name\":\"{process.ProcessName}\"}}"+
                "},");
            if (cliOptions.OptMergethreads)
            {
                converted.WriteLine("{"+
                    $"\"name\":\"thread_name\",\"ph\":\"M\",\"pid\":{process.ProcessId},\"tid\":1,"+
                    "\"args\":{\"name\":\"(merged)\"}"+
                    "},");
            }
        }

        var start = rwEvent.CaptureTime/10 - baseTime;
        var end = start + (ulong)rwEvent.DurationSpan.Ticks/10;

        if (cliOptions.OptMergethreads)
        {
            converted.WriteLine("{"+
                $"\"name\":\"{rwEvent.Operation}\",\"cat\":\"file_io\","+
                $"\"ph\":\"b\",\"pid\":{process.ProcessId},\"tid\":{1},\"ts\":{start},\"id\":{pairId},"+
                $"\"args\":{{\"path\":\"{rwEvent.Path.Replace('\\', '/')}\",\"eidx\":{rwEvent.EventIndex}}}"+
                "},");
            converted.WriteLine("{"+
                $"\"name\":\"{rwEvent.Operation}\",\"cat\":\"file_io\","+ // tracing needs both name and cat to be here also (docs claim key is cat+scope+
                $"\"ph\":\"e\",\"pid\":{process.ProcessId},\"tid\":{1},\"ts\":{end},\"id\":{pairId}"+
                "},");
            ++pairId;
        }
        else
        {
            converted.WriteLine("{"+
                $"\"name\":\"{rwEvent.Operation}\",\"cat\":\"file_io\","+
                $"\"ph\":\"B\",\"pid\":{process.ProcessId},\"tid\":{rwEvent.ThreadId},\"ts\":{start},\"cname\":\"{(rwEvent.Path.Contains("ArtifactDB") ? "bad" : "good")}\","+
                $"\"args\":{{\"path\":\"{rwEvent.Path.Replace('\\', '/')}\",\"eidx\":{rwEvent.EventIndex}}}"+
                "},");
            converted.WriteLine("{"+
                $"\"ph\":\"E\",\"pid\":{process.ProcessId},\"tid\":{rwEvent.ThreadId},\"ts\":{end}"+
                "},");
        }
    }
}

return 0;
