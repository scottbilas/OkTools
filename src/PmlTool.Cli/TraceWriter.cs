using System.Diagnostics;

class TraceWriter : IDisposable
{
    readonly StreamWriter _writer;
    int _depth;
    bool _continuing;

    TraceWriter(string path)
    {
        _writer = File.CreateText(path);
        _writer.WriteLine('[');
    }

    public static TraceWriter CreateJsonFile(string path) => new TraceWriter(path);

    public void Dispose() => _writer.Dispose();

    public TraceWriter Open()
    {
        _writer.Write('{');
        _continuing = false;
        ++_depth;
        return this;
    }

    public TraceWriter Open(string name)
    {
        if (_continuing)
            _writer.Write(',');
        _writer.Write($"\"{name}\":");
        return Open();
    }

    TraceWriter Write<T>(string name, T value, bool quoted)
    {
        if (_continuing)
            _writer.Write(',');
        _writer.Write(quoted ? $"\"{name}\":\"{value}\"" : $"\"{name}\":{value}");
        _continuing = true;
        return this;
    }

    public TraceWriter Write(string name, string value) => Write(name, value, true);
    public TraceWriter Write(string name, char value) => Write(name, value, true);
    public TraceWriter Write(string name, int value) => Write(name, value, false);
    public TraceWriter Write(string name, uint value) => Write(name, value, false);
    public TraceWriter Write(string name, ulong value) => Write(name, value, false);

    public TraceWriter Close()
    {
        _writer.Write('}');
        if (--_depth == 0)
        {
            _writer.WriteLine(',');
            Debug.Assert(_depth >= 0);
        }
        else
            _continuing = true;

        return this;
    }

    public void WriteProcessMetadata(uint processId, string processName)
    {
        Debug.Assert(_depth == 0);
        Open();
            Write("name", "process_name");
            Write("ph", "M");
            Write("pid", processId);
            Open("args");
                Write("name", processName);
            Close();
        Close();
        Debug.Assert(_depth == 0);
    }

    public void WriteThreadMetadata(uint processId, uint threadId, string threadName)
    {
        Debug.Assert(_depth == 0);
        Open();
            Write("name", "thread_name");
            Write("ph", "M");
            Write("pid", processId);
            Write("tid", threadId);
            Open("args");
                Write("name", threadName);
            Close();
        Close();
        Debug.Assert(_depth == 0);
    }
}
