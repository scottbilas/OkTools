using System.Threading.Channels;

// currently expecting main thread only for processing and processed line access. move to tasks later when needed.
class LogModel
{
    readonly ChannelReader<LogBatch> _reader;
    readonly CancellationTokenSource _cancel = new();
    readonly List<LogProcessorBase> _processors = new();
    readonly List<uint> _lastVersions = new();

    public LogModel(string logFilePath)
    {
        var channel = Channel.CreateUnbounded<LogBatch>();
        Task.Run(() => LogSource.TailFileAsync(logFilePath, channel.Writer, _cancel.Token));

        _reader = channel.Reader;
    }

    public void Dispose()
    {
        _cancel.Cancel();
    }

    public ValueTask<bool> WaitForNeedsUpdateAsync() => _reader.WaitToReadAsync(_cancel.Token);

    public void Update()
    {
        for (var i = 1; i < _processors.Count; ++i)
        {
            if (_lastVersions[i] == _processors[i].Version)
                continue;

            for (; i < _processors.Count; ++i)
            {
                _processors[i].Invalidate();
                _processors[i].Process(_processors[i-1].LinesSegment);
                _lastVersions[i] = _processors[i].Version;
            }
            break;
        }

        while (_reader.TryRead(out var change))
        {
            foreach (var processor in _processors)
                change = processor.Process(change);
        }
    }

    public void Add(LogProcessorBase processor)
    {
        _processors.Add(processor);
        _lastVersions.Add(processor.Version-1); // will resolve at update
    }

    public int GetItemCount(int processorIndex) => _processors[processorIndex].Lines.Length;
}
