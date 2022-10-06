using System.Diagnostics;
using System.Text;

class TraceWriter : IDisposable
{
    int _depth;
    bool _continuing;

    TraceWriter(string path)
    {
        Writer = File.CreateText(path);
        Writer.WriteLine('[');
    }

    public static TraceWriter CreateJsonFile(string path) => new(path);

    public void Dispose() => Writer.Dispose();

    public TraceWriter Open()
    {
        Writer.Write('{');
        _continuing = false;
        ++_depth;
        return this;
    }

    public TraceWriter Open(string name)
    {
        if (_continuing)
            Writer.Write(',');
        Writer.Write($"\"{name}\":");
        return Open();
    }

    public StreamWriter Writer { get; }

    TraceWriter Write<T>(string name, T value, bool quoted)
    {
        if (_continuing)
            Writer.Write(',');

        Writer.Write('\"');
        Writer.Write(name);

        if (quoted)
        {
            Writer.Write("\":\"");
            Writer.Write(value);
            Writer.Write('\"');
        }
        else
        {
            Writer.Write("\":");
            Writer.Write(value);
        }

        _continuing = true;
        return this;
    }

    public TraceWriter Write(string name, string value) => Write(name, value, true);
    public TraceWriter Write(string name, char value) => Write(name, value, true);
    public TraceWriter Write(string name, int value) => Write(name, value, false);
    public TraceWriter Write(string name, uint value) => Write(name, value, false);
    public TraceWriter Write(string name, long value) => Write(name, value, false);
    public TraceWriter Write(string name, ulong value) => Write(name, value, false);

    public TraceWriter Write(string name, StringBuilder value)
    {
        if (_continuing)
            Writer.Write(',');

        Writer.Write('\"');
        Writer.Write(name);
        Writer.Write("\":\"");

        foreach (var chunk in value.GetChunks())
            Writer.Write(chunk.Span);

        Writer.Write('\"');

        _continuing = true;
        return this;
    }

    public TraceWriter Close()
    {
        Writer.Write('}');
        if (--_depth == 0)
        {
            Writer.WriteLine(',');
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
