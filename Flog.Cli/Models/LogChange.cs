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
