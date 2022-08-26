using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OkTools.ProcMonUtils;

[DebuggerDisplay("PID={ProcessId}; Frames={Frames.Length}")]
public struct EventRecord
{
    public int Sequence;
    public DateTime CaptureTime;
    public int ProcessId;
    public FrameRecord[] Frames;
}

public enum FrameType
{
    Kernel,
    User,
    Mono,
}

public struct FrameRecord
{
    public FrameType Type;
    public int ModuleStringIndex;
    public int SymbolStringIndex;
    public ulong Offset; // will be the full address if no symbol
}

public class PmlBakedParseException : Exception
{
    public PmlBakedParseException(string message) : base(message) { }
}

public class PmlQuery
{
    readonly List<string> _strings = new();
    readonly Dictionary<DateTime, int> _eventsByTime = new();
    readonly Dictionary<string, List<int>> _symbolsToEvents = new();
    readonly Dictionary<string, List<int>> _modulesToEvents = new();

    EventRecord[] _events;

    enum State { Seeking, Config, Events, Strings }

    public PmlQuery(string pmlBakedPath)
    {
        Load(pmlBakedPath);

        if (_events == null)
            throw new FileLoadException($"No events found in {pmlBakedPath}");

        foreach (var evt in _events)
        {
            // uninitialized events will happen when there are gaps in the sequencing
            if (evt.ProcessId == 0)
                continue;

            foreach (var frame in evt.Frames)
            {
                Add(_modulesToEvents, evt.Sequence, frame.ModuleStringIndex);
                Add(_symbolsToEvents, evt.Sequence, frame.SymbolStringIndex);
            }
        }
    }

    public string GetString(int stringIndex) => _strings[stringIndex];

    void Add(Dictionary<string, List<int>> dict, int eventIndex, int stringIndex)
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
                                _events = new EventRecord[eventCount];
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

        var sequence = (int)parser.ReadULongHex();
        ref var eventRecord = ref _events[sequence];
        eventRecord.Sequence = sequence;
        parser.Expect(':');

        eventRecord.CaptureTime = DateTime.FromFileTime((long)parser.ReadULongHex());
        _eventsByTime[eventRecord.CaptureTime] = sequence;
        parser.Expect(';');

        eventRecord.ProcessId = (int)parser.ReadULongHex();
        eventRecord.Frames = new FrameRecord[parser.Count(';')];

        for (var iframe = 0; iframe < eventRecord.Frames.Length; ++iframe)
        {
            parser.Expect(';');

            var typec = parser.ReadChar();
            var type = typec switch
            {
                'K' => FrameType.Kernel,
                'U' => FrameType.User,
                'M' => FrameType.Mono,
                _ => throw new ArgumentOutOfRangeException($"Unknown type '{typec}' for frame {iframe}")
            };

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

    public IReadOnlyList<EventRecord> AllRecords => _events;

    public EventRecord? FindRecordBySequence(int sequence)
    {
        ref var e = ref _events[sequence];
        if (e.ProcessId != 0)
            return e;

        return null;
    }

    public EventRecord? FindRecordByCaptureTime(DateTime dateTime) => _eventsByTime.TryGetValue(dateTime, out var foundIndex) ? _events[foundIndex] : null;

    static IEnumerable<int> MatchRecordsByText(IEnumerable<KeyValuePair<string, List<int>>> items, Regex regex) =>
        items.Where(kv => regex.IsMatch(kv.Key)).SelectMany(kv => kv.Value).Distinct();

    public IEnumerable<int> MatchRecordsBySymbol(Regex regex) => MatchRecordsByText(_symbolsToEvents, regex);
    public IEnumerable<int> MatchRecordsByModule(Regex regex) => MatchRecordsByText(_modulesToEvents, regex);
}
