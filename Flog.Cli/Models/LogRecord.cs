enum LogType
{
    Source,
    Status,
    Error,
}

// note for LogRecord and LogBatch: these are mutable so we can avoid allocating temporary arrays when processing them.

readonly struct LogRecord
{
    // ulong id
    // ulong parentIdStart, parentIdEnd -- for maintaining scroll position when upstream filter changes, also associating a derived structure from multiple log entries that made it up (for example assetdb perf dump)
    // ^^^ note that a refresh will just assign all new id's...we need a way to keep them local to a source. maybe have an id == int + long where int == source serial and long == local lines serial.

    public readonly LogType LogType;

//    public readonly DateTime Timestamp; // TODO: file date if non-tail, system time if tail, processor time if time-prefix on log entries (like unity with UNITY_EXT_LOGGING)
    public readonly Memory<char> DisplayLine;
//    public readonly OkSegmentRO<(string, object)> Properties; // and tags!
    // 'stack' -> [StackTrace]

    public LogRecord(LogType logType, OkSegment<string> lines)
    {
        LogType = logType;
        Lines = lines;
    }

    public static LogRecord Source(string source) => new(LogType.Source, source);
    public static LogRecord Source(OkSegment<string> source) => new(LogType.Source, source);
    public static LogRecord Status(string status) => new(LogType.Status, status);
    public static LogRecord Error(string error) => new(LogType.Error, error);
}

readonly struct LogBatch
{
    public readonly OkSegment<LogRecord> Appended;
    public readonly bool IsClear;

    public LogBatch(OkSegment<LogRecord> appended)
    {
        Appended = appended;
        IsClear = false;
    }

    LogBatch(bool clear)
    {
        Appended = default;
        IsClear = clear;
    }

    public static LogBatch Clear => new(true);

    public static implicit operator LogBatch(OkSegment<LogRecord> appended) => new(appended);
    public static implicit operator LogBatch(LogRecord appended) => new(appended);
}
