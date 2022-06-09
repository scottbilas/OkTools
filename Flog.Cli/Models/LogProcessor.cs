

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
*/

using System.Diagnostics;

readonly struct LogChange
{
    public readonly ReadOnlyMemory<string> Appended;
    public readonly bool IsClear;

    public LogChange(ReadOnlyMemory<string> appended)
    {
        Appended = appended;
        IsClear = false;
    }

    LogChange(bool clear)
    {
        Appended = default;
        IsClear = clear;
    }

    public static LogChange Clear => new(true);

    public static implicit operator LogChange(string[] appended) => new(appended);
    public static implicit operator LogChange(ReadOnlyMemory<string> appended) => new(appended);
    public static implicit operator LogChange(Memory<string> appended) => new(appended);
}

abstract class LogProcessor
{
    public const int DefaultCapacity = 10000;

    readonly OkList<string> _processed = new(DefaultCapacity);
    uint _version = 1;

    public LogChange Process(LogChange change)
    {
        if (change.IsClear)
        {
            Invalidate();
            return change;
        }

        var oldCount = _processed.Count;
        foreach (var item in change.Appended.Span)
        {
            var processed = Process(item);
            if (processed != null)
                _processed.Add(processed);
        }

        return _processed.AsMemory[oldCount..];
    }

    //protected abstract void Process(ReadOnlySpan<string> source, Span<string> converted);
    protected abstract string? Process(string entry);

    public int Count => _processed.Count;
    public uint Version => _version;
    public ReadOnlySpan<string> Lines => _processed.AsSpan;

    public void Invalidate()
    {
        _processed.ClearItems(DefaultCapacity);
        ++_version;
    }
}

class PassThruProcessor : LogProcessor
{
    protected override string Process(string entry) => entry;
}

class SimpleFilterProcessor : LogProcessor
{
    public string Filter { get; set; } = "";

    protected override string? Process(string entry)
    {
        // TODO: loads of options here, like regex, whole word, case, etc.
        //       other inspirations for filtering:
        //        * ripgrep
        //        * voidtools Everything
        //        * sysinternals procmon
        //        * wireshark

        return entry.Contains(Filter, StringComparison.OrdinalIgnoreCase) ? entry : null;
    }
}
