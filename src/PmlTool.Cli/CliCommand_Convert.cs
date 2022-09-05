using NiceIO;
using OkTools.ProcMonUtils;

static partial class Program
{
    static CliExitCode Convert(PmlToolCliArguments cliOptions)
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

        return CliExitCode.Success;
    }
}
