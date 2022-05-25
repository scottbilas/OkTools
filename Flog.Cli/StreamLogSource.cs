using NiceIO;

/*
readonly struct LogEntry
{
    // ulong id
    // ulong parentIdStart, parentIdEnd -- for maintaining scroll position when upstream filter changes, also associating a derived structure from multiple log entries that made it up (for example assetdb perf dump)
    // ^^^ note that a refresh will just assign all new id's...we need a way to keep them local to a source. maybe have an id == int + long where int == source serial and long == local lines serial.

    // better as Memory<char> but later on probably want to add processed structural data to it (for example stack trace)
    public readonly string Text;

    public LogEntry(string text)
    {
        Text = text;
    }
}

readonly struct LogChange
{
    Span<LogEntry> Appended; // length of 0 means "full reset"
}
*/

interface ILogSource
{
    // this should be unbounded, single reader/writer
    //ChannelReader<LogChange> Changes;

    IReadOnlyList<string> Lines { get; }
}

class StreamLogSource : ILogSource
{
    readonly string[] _lines;

    public StreamLogSource(NPath path)
    {
        // TODO: start up tail-task

        _lines = path.ReadAllLines();
    }

    public IReadOnlyList<string> Lines => _lines;
}
