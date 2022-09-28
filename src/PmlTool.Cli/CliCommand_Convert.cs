using System.Text;
using NiceIO;
using OkTools.ProcMonUtils;

// ReSharper disable CommentTypo StringLiteralTypo

// TODO's for next options:
//
// * merge-processes: merge all processes with the same name (good for multicore stuff like lld.exe)
// * strip-idle: strip out idle time with min size x (good for when a dialog pops up mid-import and i don't notice it for a while)
// * special coloring for "compiling burst", "compiling shader", "compiling assembly"
// * update the filtering to catch the compiler. seems to be missing.

static partial class Program
{
    const string k_convertExtraHelp = @"
# pmltool convert

Render file IO events from the PML to CONVERTED in Chrome trace format (*). Go to `chrome://tracing` in Chrome or Edge
and load the converted .json file to visualize and browse file read/write operations. The converted file will be
overwritten if it already exists.

The rendering is highly Unity-specific currently, in terms of grouping/coloring and filtering.

Note that this file is not a 'real' tracing file - it has only been tested with Edge and is not going to have a lot of
data that other tools will expect and a normal trace run recorded by the browser will include. Tools like Perfetto or
Speedscope will not be able to read it.

(*) The Chrome trace format is at https://tinyurl.com/chrome-tracing
";

/*  https://github.com/catapult-project/catapult/blob/master/tracing/tracing/base/color_scheme.html#L30

    thread_state_uninterruptible: new tr.b.Color(182, 125, 143),
    thread_state_iowait: new tr.b.Color(255, 140, 0),
    thread_state_running: new tr.b.Color(126, 200, 148),
    thread_state_runnable: new tr.b.Color(133, 160, 210),
    thread_state_sleeping: new tr.b.Color(240, 240, 240),
    thread_state_unknown: new tr.b.Color(199, 155, 125),

    background_memory_dump: new tr.b.Color(0, 180, 180),
    light_memory_dump: new tr.b.Color(0, 0, 180),
    detailed_memory_dump: new tr.b.Color(180, 0, 180),

    vsync_highlight_color: new tr.b.Color(0, 0, 255),
    generic_work: new tr.b.Color(125, 125, 125),

    good: new tr.b.Color(0, 125, 0),
    bad: new tr.b.Color(180, 125, 0),
    terrible: new tr.b.Color(180, 0, 0),

    black: new tr.b.Color(0, 0, 0),
    grey: new tr.b.Color(221, 221, 221),
    white: new tr.b.Color(255, 255, 255),
    yellow: new tr.b.Color(255, 255, 0),
    olive: new tr.b.Color(100, 100, 0),

    rail_response: new tr.b.Color(67, 135, 253),
    rail_animation: new tr.b.Color(244, 74, 63),
    rail_idle: new tr.b.Color(238, 142, 0),
    rail_load: new tr.b.Color(13, 168, 97),
    startup: new tr.b.Color(230, 230, 0),

    heap_dump_stack_frame: new tr.b.Color(128, 128, 128),
    heap_dump_object_type: new tr.b.Color(0, 0, 255),
    heap_dump_child_node_arrow: new tr.b.Color(204, 102, 0),

    cq_build_running: new tr.b.Color(255, 255, 119),
    cq_build_passed: new tr.b.Color(153, 238, 102),
    cq_build_failed: new tr.b.Color(238, 136, 136),
    cq_build_abandoned: new tr.b.Color(187, 187, 187),

    cq_build_attempt_runnig: new tr.b.Color(222, 222, 75),
    cq_build_attempt_passed: new tr.b.Color(103, 218, 35),
    cq_build_attempt_failed: new tr.b.Color(197, 81, 81)
*/

    struct FileOp
    {
        readonly PmlFileSystemReadWriteEvent _rwEvent;
        List<ulong>? _children;

        public readonly string Path;
        public readonly ulong Start, End;

        public FileOp(PmlFileSystemReadWriteEvent rwEvent, ulong baseTime)
        {
            _rwEvent = rwEvent;
            Start = _rwEvent.CaptureTime/10 - baseTime;
            End = Start + (ulong)_rwEvent.DurationSpan.Ticks/10;
            Path = _rwEvent.Path.ToString(SlashMode.Forward);
        }

        public bool TryAdd(FileOp other)
        {
            if (End < other.End)
                return false;
            if (_rwEvent.ThreadId != other._rwEvent.ThreadId)
                return false;
            if (!Path.EqualsIgnoreCase(other.Path))
                return false;
            if (!_rwEvent.Operation.Equals(other._rwEvent.Operation))
                return false;

            if (_children == null)
                _children = new();
            else if (_children.Count != 0 && _children[^1] < other.End)
                return false;

            _children.Add(other.End);
            return true;
        }

        public bool TryExpire(ulong start)
        {
            if (End <= start)
                return true;

            if (_children != null)
            {
                while (_children.Count != 0 && _children[^1] <= start)
                    _children.DropBack();
            }

            return false;
        }
    }

    static CliExitCode Convert(PmlToolCliArguments opts)
    {
        var pmlPath = opts.ArgPml!.ToNPath().FileMustExist();
        using var pmlReader = new PmlReader(pmlPath);
        using var converted = TraceWriter.CreateJsonFile(opts.ArgConverted ?? pmlPath + ".json");

        var baseTime = pmlReader.GetEvent(0).CaptureTime / 10;
        var seenProcessIds = new HashSet<uint>();
        var pairId = 1;
        var fileOps = new FileOp?[25];
        var stackSb = new StringBuilder();

        SymbolicatedEventsDb? symbolicatedEventsDb = null;
        if (opts.OptIncludeStacks)
        {
            Console.Write("Loading symbolicated events db...");
            var pmlBakedPath = pmlPath.ChangeExtension(".pmlbaked").FileMustExist();
            symbolicatedEventsDb = new SymbolicatedEventsDb(pmlBakedPath);
            Console.Write("\r                                 \r");
        }

        // need this to set the start of the profile, otherwise edge tracing will shift everything to first visible as 0 offset,
        // which makes it hard to align with event offsets from `pmltool query`.
        converted
            .Open()
                .Write("name", "Begin")
                .Write("ph", "i")
                .Write("ts", 0)
                .Write("pid", 0)
                .Write("tid", 0)
                .Write("s", "g")
            .Close();

        foreach (var rwEvent in pmlReader.SelectEvents(PmlReader.Filter.FileSystem | PmlReader.Filter.Stacks | PmlReader.Filter.Details).OfType<PmlFileSystemReadWriteEvent>())
        {
            // sometimes i see a large read of a file in procmon, followed by a series of smaller and iteratively-offset
            // reads on the same file+process+thread, that total to the same size as the large one. the later reads have
            // flags on them like "Non-cached, Paging I/O". i can only guess at what the later reads are about, but none
            // of them have stacks on them, so that's an easy way to detect these extra reads. i'm going to ignore them
            // because they don't add any more information than the large read and just end up as noise in the trace.
            if (!opts.OptShowall && (rwEvent.Frames == null || rwEvent.Frames.Length == 0))
                continue;

            var process = pmlReader.ResolveProcess(rwEvent.ProcessIndex);
            if (seenProcessIds.Add(process.ProcessId))
            {
                converted.WriteProcessMetadata(process.ProcessId, process.ProcessName);
                if (opts.OptMergethreads == "all")
                    converted.WriteThreadMetadata(process.ProcessId, 1, "(merged)");
            }

            // debug feature, ignore
            if (!opts.OptShowall && rwEvent.Path.FileName.StartsWith("pmip_"))
                continue;

            var fileOp = new FileOp(rwEvent, baseTime);

            void WritePre(char phase, uint tid)
            {
                var name = rwEvent.Operation.ToString()!;
                string? color = null;

                if (name == "ReadFile")
                {
                    if (fileOp.Path.Contains("/Library/ShaderCache/shader/", StringComparison.OrdinalIgnoreCase))
                        name += " ShaderCache/shader";
                    else if (fileOp.Path.EndsWith("/Library/ArtifactDB", StringComparison.OrdinalIgnoreCase))
                        name += " ArtifactDB";
                    else if (fileOp.Path.Contains("/Library/Artifacts/", StringComparison.OrdinalIgnoreCase))
                        name += " Artifacts";
                    else if (fileOp.Path.Contains("/Library/PackageCache/", StringComparison.OrdinalIgnoreCase))
                        name += " PackageCache";
                    else if (fileOp.Path.Contains("/Data/Resources/", StringComparison.OrdinalIgnoreCase))
                        name += " Data/Resources";
                    else if (fileOp.Path.Contains("/Library/Bee/", StringComparison.OrdinalIgnoreCase))
                        name += " Bee";
                    else if (fileOp.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                             fileOp.Path.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) ||
                             fileOp.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        name += " Executable";
                    else
                        color = "grey";
                }
                else
                {
                    if (fileOp.Path.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                    {
                        name += " Log";
                        color = "bad"; // not really "bad"! just want the color
                    }
                    else
                        color = "terrible"; // not really "terrible"! just want the color
                }

                converted
                    .Open()
                        .Write("name", name)
                        .Write("cat", "file_io")
                        .Write("ph", phase)
                        .Write("pid", process.ProcessId)
                        .Write("tid", tid);

                if (color != null && phase != 'b') // async ('b') doesn't allow color
                    converted.Write("cname", color);

                // leave open
            }

            void WriteArgs()
            {
                converted
                    .Open("args")
                        .Write("path", fileOp.Path)
                        .Write("eidx", rwEvent.EventIndex)
                        .Write("cdt", rwEvent.CaptureDateTime.ToString(PmlUtils.CaptureTimeFormat))
                        .Write("tid", rwEvent.ThreadId);

                var symRecord = symbolicatedEventsDb?.GetRecord(rwEvent.EventIndex);
                if (symRecord != null)
                {
                    for (var i = 0; i < symRecord.Value.Frames.Length; ++i)
                    {
                        if (i != 0)
                            stackSb.Append("\\n");

                        ref var frame = ref symRecord.Value.Frames[i];
                        stackSb.Append($"{symRecord.Value.Frames.Length-i-1:00} {frame.Type.ToString()[0]}");
                        if (frame.ModuleStringIndex != 0)
                            stackSb.Append($" [{symbolicatedEventsDb!.GetString(frame.ModuleStringIndex)}]");

                        if (frame.SymbolStringIndex != 0)
                        {
                            stackSb.Append(' ');
                            stackSb.Append(symbolicatedEventsDb!.GetString(frame.SymbolStringIndex));
                            stackSb.Append(" +");
                        }

                        stackSb.Append($" 0x{frame.Offset:x}");
                    }

                    converted.Write("stack", stackSb);
                    stackSb.Clear();
                }

                converted
                    .Close();
            }

            int? slot = null;
            var skip = false;
            for (var i = 0; i != fileOps.Length; ++i)
            {
                var curFileOp = fileOps[i];
                if (curFileOp != null)
                {
                    // are we a sub-operation of this one?
                    if (curFileOp.Value.TryAdd(fileOp))
                    {
                        if (opts.OptShowall)
                            slot = i;
                        else
                            skip = true;
                        break;
                    }

                    // if can't expire this then we need to go to the next
                    if (!curFileOp.Value.TryExpire(fileOp.Start))
                        continue;
                }

                fileOps[i] = fileOp;
                slot = i;
                break;
            }

            if (skip)
                continue;

            if (slot == null)
                throw new OverflowException("vtidExpirePool too small");

            if (opts.OptMergethreads == "all")
            {
                // b/e is independent async event
                WritePre('b', 1);
                    converted.Write("ts", fileOp.Start);
                    converted.Write("id", pairId);
                    WriteArgs();
                converted.Close(); // close pre
                WritePre('e', 1);
                converted
                    .Write("ts", fileOp.End)
                    .Write("id", pairId)
                    .Close(); // close pre

                ++pairId;
            }
            else
            {
                var tid = opts.OptMergethreads switch
                {
                    "none" or null => rwEvent.ThreadId,
                    "min" => (uint)slot + 1,
                    _ => throw new InvalidOperationException() // should never get here unless there's a bug
                };

                // B/E is normal nested event
                WritePre('B', tid);
                    converted.Write("ts", fileOp.Start);
                    WriteArgs();
                converted.Close(); // close pre
                converted
                    .Open()
                        .Write("ph", "E")
                        .Write("pid", process.ProcessId)
                        .Write("tid", tid)
                        .Write("ts", fileOp.End)
                    .Close();
            }
        }

        return CliExitCode.Success;
    }
}
