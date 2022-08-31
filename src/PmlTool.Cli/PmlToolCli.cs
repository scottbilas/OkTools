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

    var pmlPath = cliOptions.ArgPml!.ToNPath().FileMustExist();
    var bakedFile = pmlPath.ChangeExtension(".pmlbaked");

    var iter = 0;
    PmlUtils.Symbolicate(pmlPath, new SymbolicateOptions {
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

    var pmlPath = cliOptions.ArgPml!.ToNPath().FileMustExist();
    var modulePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var iter = 0;
    using (var pmlReader = new PmlReader(pmlPath))
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
            Console.WriteLine($"fail! {e.GetType().Name}: {e.Message}");
        }
    }
}
else if (cliOptions.CmdQuery)
{
    var pmlBakedPath = cliOptions.ArgPmlbaked!.ToNPath();
    if (!pmlBakedPath.HasExtension())
        pmlBakedPath = pmlBakedPath.ChangeExtension(".pmlbaked");
    pmlBakedPath.FileMustExist();

    Console.WriteLine($"Reading {pmlBakedPath}...");
    var pmlQuery = new PmlQuery(pmlBakedPath);

    void Dump(EventRecord eventRecord)
    {
        Console.WriteLine();
        Console.WriteLine("Sequence = " + eventRecord.Sequence);
        Console.WriteLine("CaptureTime = " + eventRecord.CaptureTime.ToString(PmlUtils.CaptureTimeFormat));
        Console.WriteLine("PID = " + eventRecord.ProcessId);

        if (eventRecord.Frames.Length > 0)
        {
            Console.WriteLine("Frames:");

            var sb = new StringBuilder();

            for (var i = 0; i < eventRecord.Frames.Length; ++i)
            {
                ref var frame = ref eventRecord.Frames[i];
                sb.Append($"    {i:00} {frame.Type.ToString()[0]}");
                if (frame.ModuleStringIndex != 0)
                    sb.Append($" [{pmlQuery.GetString(frame.ModuleStringIndex)}]");

                sb.Append(frame.SymbolStringIndex != 0
                    ? $" {pmlQuery.GetString(frame.SymbolStringIndex)} + 0x{frame.Offset:x}"
                    : $" 0x{frame.Offset:x}");

                Console.WriteLine(sb);
                sb.Clear();
            }
        }
        else
            Console.WriteLine("Frames: <none>");
    }

    foreach (var query in cliOptions.ArgQuery)
    {
        if (int.TryParse(query, out var eventIdArg))
        {
            var eventRecord = pmlQuery.FindRecordBySequence(eventIdArg);
            if (eventRecord != null)
                Dump(eventRecord.Value);
            else
                Console.WriteLine("No event found matching sequence ID " + eventIdArg);
        }
        else if (DateTime.TryParse(query, out var captureTime))
        {
            var eventRecord = pmlQuery.FindRecordByCaptureTime(captureTime);
            if (eventRecord != null)
                Dump(eventRecord.Value);
            else
                Console.WriteLine("No event found matching " + captureTime.ToString(PmlUtils.CaptureTimeFormat));
        }
        else
        {
            var regex = new Regex(query);
            var eventIds = Enumerable.Concat(
                pmlQuery.MatchRecordsByModule(regex),
                pmlQuery.MatchRecordsBySymbol(regex));

            foreach (var eventId in eventIds)
            {
                var eventRecord = pmlQuery.FindRecordBySequence(eventId);
                if (eventRecord != null)
                    Dump(eventRecord.Value);
            }
        }
    }
}

return 0;
