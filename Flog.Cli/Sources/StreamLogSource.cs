using NiceIO;
using OkTools.Core;

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
        // TODO: async this in batches
        // TODO: option to tail the log (on by default)
        // TODO: option for dealing with a deleted/truncated log while reading/tailing

        _lines = path.ReadAllLines();
    }

    public IReadOnlyList<string> Lines => _lines;
}

class FilterLogSource : ILogSource
{
    readonly ILogSource _source;
    readonly List<string> _lines;
    string _filter = "";

    public FilterLogSource(ILogSource parent)
    {
        _source = parent;

        // TODO: async this in batches

        _lines = _source.Lines.ToList();
    }

    public void SetFilter(string filter)
    {
        // TODO: loads of options here, like regex, whole word, case, etc.
        //       other inspirations for filtering:
        //        * ripgrep
        //        * voidtools Everything
        //        * sysinternals procmon
        //        * wireshark

        if (_filter == filter)
            return;

        _filter = filter;
        _lines.SetRange(_source.Lines.Where(l => l.Contains(_filter)));
    }

    public IReadOnlyList<string> Lines => _lines;
}
