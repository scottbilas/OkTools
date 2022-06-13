abstract class LogProcessorBase : ILineDataSource
{
    const int k_defaultCapacity = 10000;

    readonly OkList<LogRecord> _processed = new(k_defaultCapacity);
    uint _version = 1;

    public LogBatch Process(LogBatch batch)
    {
        if (batch.IsClear)
        {
            Invalidate();
            return batch;
        }

        var oldCount = _processed.Count;
        foreach (var record in batch.Appended.Span)
        {
            var processed = Process(record);
            if (processed != null)
                _processed.Add(processed.Value);
        }

        return _processed.AsSegment[oldCount..];
    }

    protected virtual LogBatch Process(LogBatch batch)
    {
        var records = batch.Appended.Span;
        for (var i = 0; i < records.Length; )
        {
            if (Process(records[i]))
            {
                if (processed.Lines.Length != null)
                    _processed.Add(processed.Value);
            }
            else
            {
            }
        }
    }

    protected virtual bool Process(ref LogRecord record) => false;

    public int DefaultCapacity => k_defaultCapacity;
    public uint Version => _version;
    public ReadOnlySpan<LogRecord> Lines => _processed.AsSpan;
    public OkSegment<LogRecord> LinesSegment => _processed.AsSegment;

    public void Invalidate()
    {
        _processed.ClearItems(DefaultCapacity);
        ++_version;
    }
}

class PassThruProcessor : LogProcessorBase
{
    protected override LogRecord? Process(LogRecord record) => record;
}

class SimpleFilterProcessor : LogProcessorBase
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

    protected override LogRecord? Process(LogRecord record)
    {
        // TODO: loads of options here, like regex, whole word, case, etc.
        //       other inspirations for filtering:
        //        * ripgrep
        //        * voidtools Everything
        //        * sysinternals procmon
        //        * wireshark

        foreach (var line in record.Lines)
        {

        }

        return record.Lines.Any(c => c.Contains(Filter, StringComparison.OrdinalIgnoreCase) ? record : null;
    }
}
