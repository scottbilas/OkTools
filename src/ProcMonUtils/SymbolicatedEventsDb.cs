using System.Text.RegularExpressions;

namespace OkTools.ProcMonUtils;

public enum FrameType
{
    Kernel = 'K',
    User   = 'U',
    Mono   = 'M',
}

public static class FrameTypeUtils
{
    public static FrameType Parse(char ch)
    {
        if (!TryParse(ch, out var type))
            throw new ArgumentOutOfRangeException($"Unknown type '{ch}'");
        return type;
    }

    public static bool TryParse(char ch, out FrameType type)
    {
        switch (ch)
        {
            case 'K': type = FrameType.Kernel; return true;
            case 'U': type = FrameType.User; return true;
            case 'M': type = FrameType.Mono; return true;
        }

        type = default;
        return false;
    }

    public static char ToChar(this FrameType type) => (char)type;
}

// written by PmlUtils.Symbolicate when DebugFormat==true
public readonly record struct DebugFrameRecord(FrameType Type, string Module, string Symbol, int Offset, ulong Address)
{
    static readonly Regex k_debugFrameRx = new(@"
            (?<type>[KMU])
            (
              \ \[(?<module>[^]]+)\]
              \ (?<symbol>.*)
              \ \+\ 0x(?<offset>[0-9a-fA-F]+)
              \ \(0x(?<addr>[0-9a-fA-F]+)\)
            |
              \ 0x(?<addr>[0-9a-fA-F]+)
            )$", RegexOptions.IgnorePatternWhitespace);

    public static bool TryParse(string line, out DebugFrameRecord record)
    {
        var m = k_debugFrameRx.Match(line);
        if (!m.Success)
        {
            record = default;
            return false;
        }

        record = new DebugFrameRecord(
            FrameTypeUtils.Parse(m.Groups["type"].Value[0]),
            m.Groups["module"].Value,
            m.Groups["symbol"].Value,
            m.Groups["offset"].Success ? Convert.ToInt32(m.Groups["offset"].Value, 16) : 0,
            Convert.ToUInt64(m.Groups["addr"].Value, 16));

        return true;
    }
}

public class PmlBakedParseException : Exception
{
    public PmlBakedParseException(string message) : base(message) { }
}

public class SymbolicatedEventsDb
{
    readonly Dictionary<string, List<int>> _symbolsToEvents = new();
    readonly Dictionary<string, List<int>> _modulesToEvents = new();
    readonly PmlBakedReader _pmlBakedReader;

    public SymbolicatedEventsDb(string pmlBakedPath)
    {
        using (var file = File.OpenRead(pmlBakedPath))
            _pmlBakedReader = new PmlBakedReader(file);

        void Add(Dictionary<string, List<int>> dict, int eventIndex, int stringIndex)
        {
            var value = GetString(stringIndex);
            if (!dict.TryGetValue(value, out var list))
                dict.Add(value, list = new());
            list.Add(eventIndex);
        }

        for (var (eventIndex, eventCount) = (0, _pmlBakedReader.EventCount); eventIndex != eventCount; ++eventIndex)
        {
            foreach (var frame in _pmlBakedReader.GetFrames(eventIndex))
            {
                Add(_modulesToEvents, eventIndex, frame.ModuleStringIndex);
                Add(_symbolsToEvents, eventIndex, frame.SymbolStringIndex);
            }
        }
    }

    public PmlBakedReader PmlBakedReader => _pmlBakedReader;

    public ReadOnlySpan<char> GetCharSpan(int stringIndex) => _pmlBakedReader.GetCharSpan(stringIndex);
    public string GetString(int stringIndex) => new(GetCharSpan(stringIndex));
    public ReadOnlySpan<PmlBakedFrame> GetFrames(int eventIndex) => _pmlBakedReader.GetFrames(eventIndex);

    static IEnumerable<int> MatchRecordsByText(IEnumerable<KeyValuePair<string, List<int>>> items, Regex regex) =>
        items.Where(kv => regex.IsMatch(kv.Key)).SelectMany(kv => kv.Value).Distinct();

    public IEnumerable<int> MatchRecordsBySymbol(Regex regex) => MatchRecordsByText(_symbolsToEvents, regex);
    public IEnumerable<int> MatchRecordsByModule(Regex regex) => MatchRecordsByText(_modulesToEvents, regex);
}
