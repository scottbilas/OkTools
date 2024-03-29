namespace OkTools.Flog;

public interface ILineDataSource
{
    uint Version { get; }
    ReadOnlySpan<string> Lines { get; }
    int Count => Lines.Length;
    int DefaultCapacity { get; }
}

public struct LineDataSource
{
    uint _lastVersion; // detect if raw source totally changes (e.g. file truncated)

    public ILineDataSource Data { get; } // raw lines
    public int LastLineCount { get; private set; } // detect if the source has new lines available

    public LineDataSource(ILineDataSource data)
    {
        Data = data;
        _lastVersion = data.Version - 1;
        LastLineCount = 0;
    }

    public enum ChangeStatus
    {
        NoChange,
        NewLines,
        NewSource,
    }

    public ChangeStatus CheckChanged()
    {
        if (_lastVersion != Data.Version)
        {
            _lastVersion = Data.Version;
            LastLineCount = 0;
            return ChangeStatus.NewSource;
        }

        if (LastLineCount != Data.Count)
        {
            LastLineCount = Data.Count;
            return ChangeStatus.NewLines;
        }

        return ChangeStatus.NoChange;
    }
}

public abstract class LogProcessorBase : ILineDataSource
{
    const int k_defaultCapacity = 10000;

    readonly OkList<string> _processed = new(k_defaultCapacity);
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

    public int DefaultCapacity => k_defaultCapacity;
    public uint Version => _version;
    public ReadOnlySpan<string> Lines => _processed.AsSpan;
    public ReadOnlyMemory<string> LinesMemory => _processed.AsMemory;

    public void Invalidate()
    {
        _processed.Clear(DefaultCapacity);
        ++_version;
    }
}

public class PassThruProcessor : LogProcessorBase
{
    protected override string Process(string entry) => entry;
}

public class SimpleFilterProcessor : LogProcessorBase
{
    string _filter = "";

    public string Filter
    {
        get => _filter;
        set
        {
            if (_filter == value)
                return;

            _filter = value;
            Invalidate();
        }
    }

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
