using NiceIO;
using OkTools.ProcMonUtils;

static partial class Program
{
    static CliExitCode Convert(PmlToolCliArguments opts)
    {
        var pmlPath = opts.ArgPml!.ToNPath().FileMustExist();
        using var pmlReader = new PmlReader(pmlPath);
        using var converted = TraceWriter.CreateJsonFile(opts.ArgConverted ?? pmlPath + ".json");

        ulong? baseTime = null;
        var seenProcessIds = new HashSet<uint>();
        var pairId = 1;

        foreach (var rwEvent in pmlReader.SelectEvents(PmlReader.Filter.FileSystem | PmlReader.Filter.Details).OfType<PmlFileSystemReadWriteEvent>())
        {
            baseTime ??= rwEvent.CaptureTime / 10;

            var process = pmlReader.ResolveProcess(rwEvent.ProcessIndex);
            if (seenProcessIds.Add(process.ProcessId))
            {
                converted.WriteProcessMetadata(process.ProcessId, process.ProcessName);
                if (opts.OptMergethreads == "all")
                    converted.WriteThreadMetadata(process.ProcessId, 1, "(merged)");
            }

            var start = rwEvent.CaptureTime/10 - baseTime.Value;
            var end = start + (ulong)rwEvent.DurationSpan.Ticks/10;

            void WritePre(char phase, uint tid)
            {
                converted
                    .Open()
                        .Write("name", rwEvent.Operation.ToString()!)
                        .Write("cat", "file_io")
                        .Write("ph", phase)
                        .Write("pid", process.ProcessId)
                        .Write("tid", tid);
                // leave open
            }

            void WritePathArgs(string path, uint eventIndex, bool includeColor)
            {
                path = path.Replace('\\', '/');
                if (includeColor)
                    converted.Write("cname", path.Contains("/Library/Artifacts/") ? "bad" : "good");

                converted
                    .Open("args")
                        .Write("path", path)
                        .Write("eidx", eventIndex)
                    .Close();
            }

            switch (opts.OptMergethreads)
            {
                case "none":
                case null:
                    // B/E is normal nested event
                    WritePre('B', rwEvent.ThreadId);
                        converted.Write("ts", start);
                        WritePathArgs(rwEvent.Path, rwEvent.EventIndex, true);
                    converted.Close(); // close pre
                    converted
                        .Open()
                            .Write("ph", "E")
                            .Write("pid", process.ProcessId)
                            .Write("tid", rwEvent.ThreadId)
                            .Write("ts", end)
                        .Close();
                    break;

                case "all":
                    // b/e is independent async event
                    WritePre('b', 1);
                        converted.Write("ts", start);
                        converted.Write("id", pairId);
                        WritePathArgs(rwEvent.Path, rwEvent.EventIndex, false);
                    converted.Close(); // close pre
                    WritePre('e', 1);
                    converted
                        .Write("ts", end)
                        .Write("id", pairId)
                        .Close(); // close pre
                    ++pairId;
                    break;

                case "min":
                    break;
            }
        }

        return CliExitCode.Success;
    }
}
