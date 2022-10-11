using System.Diagnostics;
using System.Text.RegularExpressions;
using MessagePack;

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

public struct FrameRecord
{
    public FrameType Type;
    public int       ModuleStringIndex;
    public int       SymbolStringIndex;
    public ulong     AddressOrOffset; // will be the full address if no symbol

    public override string ToString() => SymbolStringIndex != 0
        ? $"{Type.ToChar()} [{ModuleStringIndex}] {SymbolStringIndex} + 0x{AddressOrOffset:x}"
        : $"{Type.ToChar()} [{ModuleStringIndex}] 0x{AddressOrOffset:x}";

    public string ToString(SymbolicatedEventsDb db) => SymbolStringIndex != 0
        ? $"{Type.ToChar()} [{db.GetString(ModuleStringIndex)}] {db.GetString(SymbolStringIndex)} + 0x{AddressOrOffset:x}"
        : $"{Type.ToChar()} [{db.GetString(ModuleStringIndex)}] 0x{AddressOrOffset:x}";
}

public class PmlBakedParseException : Exception
{
    public PmlBakedParseException(string message) : base(message) { }
}

public class SymbolicatedEventsDb
{
    readonly List<string> _strings = new();
    readonly Dictionary<string, List<int>> _symbolsToEvents = new();
    readonly Dictionary<string, List<int>> _modulesToEvents = new();

    SymbolicatedEvent[] _events;
    bool[] _validEvents;

    public SymbolicatedEventsDb(string pmlBakedPath)
    {
        Load(pmlBakedPath);

        if (_events == null || _validEvents == null)
            throw new FileLoadException($"No events found in {pmlBakedPath}");

        for (var i = 0; i < _events.Length; ++i)
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

    void Add(Dictionary<string, List<int>> dict, int eventIndex, int stringIndex)
    {
        var value = GetString(stringIndex);
        if (!dict.TryGetValue(value, out var list))
            dict.Add(value, list = new());
        list.Add(eventIndex);
    }

    void Load(string pmlBakedPath)
    {
        PmlBakedData data;

        using (var file = File.OpenRead(pmlBakedPath))
        {
            var bytes = new byte[PmlBakedData.PmlBakedMagic.Length];
            if (file.Read(bytes) < bytes.Length || !bytes.SequenceEqual(PmlBakedData.PmlBakedMagic))
                throw new PmlBakedParseException("Not a .pmlbaked file, or is corrupt");

            data = MessagePackSerializer.Deserialize<PmlBakedData>(file,
                MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));
        }

        _strings.SetRange(data.Strings);

        var eventCount = data.Frames[^1].EventIndex + 1;
        _events = new SymbolicatedEvent[eventCount];
        _validEvents = new bool[eventCount];

        foreach (var (eventIndex, begin, end) in data.SelectFrameRanges())
        {
            _validEvents[eventIndex] = true;

            ref var eventRecord = ref _events[eventIndex];

            var count = end - begin;
            eventRecord.Frames = new FrameRecord[count];

            for (var i = 0; i != count; ++i)
            {
                var bakedFrame = data.Frames[begin + i];
                eventRecord.Frames[i] = new FrameRecord
                {
                    Type = bakedFrame.FrameType,
                    ModuleStringIndex = bakedFrame.ModuleIndex,
                    SymbolStringIndex = bakedFrame.SymbolIndex,
                    AddressOrOffset = bakedFrame.AddressOrOffset,
                };
            }
        }
    }

    public SymbolicatedEvent? GetRecord(int eventIndex) =>
        _validEvents[eventIndex] ? _events[eventIndex] : null;

    static IEnumerable<int> MatchRecordsByText(IEnumerable<KeyValuePair<string, List<int>>> items, Regex regex) =>
        items.Where(kv => regex.IsMatch(kv.Key)).SelectMany(kv => kv.Value).Distinct();

    public IEnumerable<int> MatchRecordsBySymbol(Regex regex) => MatchRecordsByText(_symbolsToEvents, regex);
    public IEnumerable<int> MatchRecordsByModule(Regex regex) => MatchRecordsByText(_modulesToEvents, regex);
}
