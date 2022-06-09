using System.Threading.Channels;

class LogFilterChain
{
    readonly ChannelReader<LogChange> _source;
    readonly List<LogProcessor> _processors = new();

    public LogFilterChain(ChannelReader<LogChange> source)
    {
        _source = source;
    }

    public void Process()
    {
        while (_source.TryRead(out var change))
        {
            foreach (var processor in _processors)
                change = processor.Process(change);
        }
    }

    public void Add(LogProcessor processor) => _processors.Add(processor);
    public int GetItemCount(int processorIndex) => _processors[processorIndex].Count;
}
