using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OkTools.ProcMonUtils;

[DebuggerDisplay("Frames={Frames.Length}")]
public struct SymbolicatedEvent
{
    public FrameRecord[] Frames;
}

public enum FrameType
{
    Kernel = 'K',
    User   = 'U',
    Mono   = 'M',
}

static class FrameTypeUtils
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

[DebuggerDisplay("{Type} {ModuleStringIndex}!{SymbolStringIndex}+{Offset}")]
public struct FrameRecord
{
    public FrameType Type;
    public int       ModuleStringIndex;
    public int       SymbolStringIndex;
    public ulong     Offset; // will be the full address if no symbol
}

public class PmlBakedParseException : Exception
{
    public PmlBakedParseException(string message) : base(message) { }
}

public class SymbolicatedEventsDb
{
    readonly List<string> _strings = new();
    readonly Dictionary<string, List<uint>> _symbolsToEvents = new();
    readonly Dictionary<string, List<uint>> _modulesToEvents = new();

    SymbolicatedEvent[] _events;
    bool[] _validEvents;

    enum State { Seeking, Config, Events, Strings }

    public SymbolicatedEventsDb(string pmlBakedPath)
    {
        Load(pmlBakedPath);

        if (_events == null || _validEvents == null)
            throw new FileLoadException($"No events found in {pmlBakedPath}");

        for (var i = 0u; i < _events.Length; ++i)
        {
            if (!_validEvents[i])
                continue;

            foreach (var frame in _events[i].Frames)
            {
                Add(_modulesToEvents, i, frame.ModuleStringIndex);
                Add(_symbolsToEvents, i, frame.SymbolStringIndex);
            }
        }
    }

    public string GetString(int stringIndex) => _strings[stringIndex];

    void Add(Dictionary<string, List<uint>> dict, uint eventIndex, int stringIndex)
    {
        var value = GetString(stringIndex);
        if (!dict.TryGetValue(value, out var list))
            dict.Add(value, list = new());
        list.Add(eventIndex);
    }

    void Load(string pmlBakedPath)
    {
        var lines = File
            .ReadLines(pmlBakedPath)
            .Select((text, index) => (text: text.Trim(), index + 1))
            .Where(l => l.text.Length != 0 && l.text[0] != '#');

        var (state, currentLine) = (State.Seeking, 0);

        try
        {
            foreach (var (line, index) in lines)
            {
                currentLine = index;

                if (line[0] == '[')
                {
                    state = line switch
                    {
                        "[Config]" => State.Config,
                        "[Events]" => State.Events,
                        "[Strings]" => State.Strings,
                        _ => throw new PmlBakedParseException($"Not a supported section {line}")
                    };
                    continue;
                }

                switch (state)
                {
                    case State.Seeking:
                        throw new PmlBakedParseException("Unexpected lines without category");

                    case State.Config:
                        var m = Regex.Match(line, @"(\w+)\s*=\s*(\w+)");
                        if (!m.Success)
                            throw new PmlBakedParseException($"Unexpected config format: {line}");
                        switch (m.Groups[1].Value)
                        {
                            case "EventCount":
                                var eventCount = int.Parse(m.Groups[2].Value);
                                _events = new SymbolicatedEvent[eventCount];
                                _validEvents = new bool[eventCount];
                                break;
                            case "DebugFormat":
                                if (bool.Parse(m.Groups[2].Value))
                                    throw new PmlBakedParseException("DebugFormat=true not supported for querying");
                                break;
                            default:
                                throw new PmlBakedParseException($"Unexpected config option: {m.Groups[1].Value}");
                        }
                        break;

                    case State.Events:
                        ParseEventLine(line);
                        break;

                    case State.Strings:
                        var parser = new SimpleParser(line);
                        var stringIndex = (int)parser.ReadULongHex();
                        if (stringIndex != _strings.Count)
                            throw new InvalidOperationException("Mismatch string index");
                        parser.Expect(':');
                        _strings.Add(parser.AsSpan().ToString());
                        break;
                }
            }
        }
        catch (Exception x)
        {
            throw new FileLoadException($"{pmlBakedPath}({currentLine}): {x.Message}", x);
        }
    }

    void ParseEventLine(string line)
    {
        var parser = new SimpleParser(line);

        var eventIndex = (uint)parser.ReadULongHex();
        _validEvents[eventIndex] = true;

        ref var eventRecord = ref _events[eventIndex];
        eventRecord.Frames = new FrameRecord[parser.Count(';')];

        for (var iframe = 0; iframe < eventRecord.Frames.Length; ++iframe)
        {
            parser.Expect(';');

            var ch = parser.ReadChar();
            if (!FrameTypeUtils.TryParse(ch, out var type))
                throw new ArgumentOutOfRangeException($"Unknown type '{ch}' for frame {iframe}");

            parser.Expect(',');
            var first = parser.ReadULongHex();

            switch (parser.PeekCharSafe())
            {
                // non-symbol frame
                case ';':
                case '\0':
                    eventRecord.Frames[iframe] = new FrameRecord
                        { Type = type, Offset = first, };
                    break;

                // symbol frame
                case ',':
                    ref var frameRecord = ref eventRecord.Frames[iframe];
                    frameRecord.Type = type;
                    frameRecord.ModuleStringIndex = (int)first;
                    parser.Advance(1); // already read
                    frameRecord.SymbolStringIndex = (int)parser.ReadULongHex();
                    parser.Expect(',');
                    frameRecord.Offset = parser.ReadULongHex();
                    break;

                default:
                    throw new PmlBakedParseException("Parse error");
            }
        }

        if (!parser.AtEnd)
            throw new PmlBakedParseException("Unexpected extra frames");
    }

    public SymbolicatedEvent? GetRecord(uint eventIndex) =>
        _validEvents[eventIndex] ? _events[eventIndex] : null;

    static IEnumerable<uint> MatchRecordsByText(IEnumerable<KeyValuePair<string, List<uint>>> items, Regex regex) =>
        items.Where(kv => regex.IsMatch(kv.Key)).SelectMany(kv => kv.Value).Distinct();

    public IEnumerable<uint> MatchRecordsBySymbol(Regex regex) => MatchRecordsByText(_symbolsToEvents, regex);
    public IEnumerable<uint> MatchRecordsByModule(Regex regex) => MatchRecordsByText(_modulesToEvents, regex);
}
