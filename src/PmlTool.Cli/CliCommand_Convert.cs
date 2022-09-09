using NiceIO;
using OkTools.ProcMonUtils;

static partial class Program
{
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

    record FileOp(string Path, ulong Expire)
    {
        List<ulong>? _children;

        public bool TryAdd(string path, ulong end)
        {
            if (Expire < end)
                return false;
            if (!Path.EqualsIgnoreCase(path))
                return false;

            if (_children == null)
                _children = new();
            else if (_children[^1] < end)
                return false;

            _children.Add(end);
            return true;
        }

        public bool TryExpire(ulong start)
        {
            if (Expire <= start)
                return true;

            if (_children != null)
            {
                while (_children[^1] <= start)
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
            if (rwEvent.Frames == null)
                continue;

            var process = pmlReader.ResolveProcess(rwEvent.ProcessIndex);
            if (seenProcessIds.Add(process.ProcessId))
            {
                converted.WriteProcessMetadata(process.ProcessId, process.ProcessName);
                if (opts.OptMergethreads == "all")
                    converted.WriteThreadMetadata(process.ProcessId, 1, "(merged)");
            }

            var start = rwEvent.CaptureTime/10 - baseTime;
            var end = start + (ulong)rwEvent.DurationSpan.Ticks/10;

            void WritePre(char phase, uint tid)
            {
                converted
                    .Open()
                        .Write("name", $"{rwEvent.Operation} {rwEvent.Path.Parent.FileName}/{rwEvent.Path.FileName}")
                        .Write("cat", "file_io")
                        .Write("ph", phase)
                        .Write("pid", process.ProcessId)
                        .Write("tid", tid);
                // leave open
            }

            void WriteArgs(bool includeColor)
            {
                var path = rwEvent.Path.ToString(SlashMode.Forward);
                if (includeColor)
                {
                    if (path.EndsWith("/Library/ArtifactDB"))
                        converted.Write("cname", "yellow");
                    else if (path.Contains("/Library/Artifacts/"))
                        converted.Write("cname", "olive");
                }

                converted
                    .Open("args")
                        .Write("path", path)
                        .Write("eidx", rwEvent.EventIndex)
                        .Write("cdt", rwEvent.CaptureDateTime.ToString(PmlUtils.CaptureTimeFormat))
                    .Close();
            }

            void WriteEvent(uint tid)
            {
                // B/E is normal nested event
                WritePre('B', tid);
                    converted.Write("ts", start);
                    WriteArgs(true);
                converted.Close(); // close pre
                converted
                    .Open()
                        .Write("ph", "E")
                        .Write("pid", process.ProcessId)
                        .Write("tid", tid)
                        .Write("ts", end)
                    .Close();
            }

            switch (opts.OptMergethreads)
            {
                case "none":
                case null:
                    WriteEvent(rwEvent.ThreadId);
                    break;

                case "min":

                    var best = (index: -1, expire: (ulong?)null);

                    for (var i = 0; i < fileOps.Length; ++i)
                    {
                        var fileOp = fileOps[i];
                        if (fileOp != null)
                        {
                            // are we a sub-operation of this one?
                            if (fileOp.TryAdd(rwEvent.Path, end))
                            {
                                best.index = i;
                                break;
                            }

                            // can we expire this one?
                            if (fileOp.TryExpire(start))
                            {
                                if (best.expire == null || fileOp.Expire < best.expire)
                                    best = (i, fileOp.Expire);
                                fileOp = fileOps[i] = null;
                            }
                        }

                        if (fileOp == null && best.index == -1)
                            best = (i, null);
                    }

                    if (best.index < 0)
                        throw new OverflowException("vtidExpirePool too small");

                    WriteEvent((uint)best.index + 1);
                    break;

                case "all":
                    // b/e is independent async event
                    WritePre('b', 1);
                        converted.Write("ts", start);
                        converted.Write("id", pairId);
                        WriteArgs(false); // async doesn't support color
                    converted.Close(); // close pre
                    WritePre('e', 1);
                    converted
                        .Write("ts", end)
                        .Write("id", pairId)
                        .Close(); // close pre
                    ++pairId;
                    break;

                default:
                    throw new InvalidOperationException(); // should never get here unless there's a bug
            }
        }

        return CliExitCode.Success;
    }
}
