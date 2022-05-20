using System.Text;
using System.Threading.Tasks.Dataflow;
using Vezel.Cathode.Text.Control;

enum InputParseResult { Accept, NoMatch, Partial }

class InputParser
{
    static readonly TimeSpan k_standaloneEscTimeoutMs = TimeSpan.FromMilliseconds(50); // tcell uses this timeout

    readonly BufferBlock<ReadOnlyMemory<byte>> _rawInput;
    readonly BufferBlock<IEvent> _events;

    readonly ByteStream _input = new();
    bool _inEscape;
    DateTime _keyExpire;

    public InputParser(BufferBlock<ReadOnlyMemory<byte>> rawInput, BufferBlock<IEvent> events)
    {
        _rawInput = rawInput;
        _events = events;
    }

    public void Process()
    {
        var oldSize = _input.Count;

        // TODO: would be better to figure out how to Receive() with a timeout

        // block until we get something, unless we're in a partial sequence (in which case sleep to avoid spinning for the full timeout)
        if (_input.Count == 0 && !_inEscape)
            _input.AddRange(_rawInput.Receive());
        else
            Thread.Sleep(1);

        // read whatever more is available
        while (_rawInput.TryReceive(out var chunk))
            _input.AddRange(chunk);

        // only consider timer expired if we still haven't gotten any new input
        if (_input.Count != oldSize)
            Parse(false);
        else if (DateTime.UtcNow >= _keyExpire)
            Parse(true);
    }

    void Parse(bool timerExpired)
    {
        for (;;)
        {
            if (_input.IsEmpty)
            {
                _input.Reset(); // we've consumed all input, so reuse the memory for further writes
                break;
            }

            var partial = false;

            switch (ParseChar())
            {
                case InputParseResult.Accept: continue;
                case InputParseResult.Partial: partial = true; break;
            }

            switch (ParseControlKey())
            {
                case InputParseResult.Accept: continue;
                case InputParseResult.Partial: partial = true; break;
            }

            // may have caught in the middle of an escape sequence, so wait for more input (or a timeout)
            if (partial && !timerExpired)
                break;

            var b = _input.Read();
            if (b != ControlConstants.ESC)
            {
                // unrecognized sequence or timeout, so push to app to figure it out
                _events.Post(new KeyEvent((char)b, _inEscape, false));
                _inEscape = false;
            }
            else if (_input.IsEmpty)
            {
                // this is a real esc keypress (we likely got here because ESC+timeout)
                // note that (at least on windows) no modifiers will come through, as the OS uses modified ESC for other things
                _events.Post(new KeyEvent(ConsoleKey.Escape));
                _inEscape = false;
            }
            else
            {
                // could be alt-char; keep going
                // TODO: what happens if the user hammers or holds down ESC?
                _inEscape = true;
            }
        }

        _keyExpire = DateTime.UtcNow + k_standaloneEscTimeoutMs;
    }

    InputParseResult ParseChar()
    {
        var b = _input.Peek();
        if (b >= ' ' && b <= 0x7F)
        {
            // normal ascii

            _events.Post(new KeyEvent((char)b, _inEscape, false));
            _inEscape = false;
            _input.Skip();

            return InputParseResult.Accept;
        }

        // control keys
        if (b < 0x80)
            return InputParseResult.NoMatch;

        // if we get here, we must be receiving utf-8, which we don't support (yet)
        throw new NotImplementedException("UTF-8 not supported");
    }

    readonly struct ControlMapping
    {
        public readonly byte[] Input;
        public readonly ConsoleKeyInfo Key;

        public ControlMapping(string input, ConsoleKey key, bool shift = false, bool alt = false, bool ctrl = false)
        {
            Input = Encoding.ASCII.GetBytes(input);
            Key = new ConsoleKeyInfo((char)0, key, shift, alt, ctrl);
        }

        public ControlMapping(string input, char ch, bool alt = false, bool ctrl = false)
        {
            Input = Encoding.ASCII.GetBytes(input);
            Key = new ConsoleKeyInfo(ch, 0, char.IsUpper(ch), alt, ctrl);
        }
    }

    static readonly ControlMapping[] k_controlMappings =
    {
        new("\x1b[A",    ConsoleKey.UpArrow),
        new("\x1b[1;5A", ConsoleKey.UpArrow,    ctrl:true),
        new("\x1b[B",    ConsoleKey.DownArrow),
        new("\x1b[1;5B", ConsoleKey.DownArrow,  ctrl:true),
        new("\x1b[C",    ConsoleKey.RightArrow),
        new("\x1b[1;5C", ConsoleKey.RightArrow, ctrl:true),
        new("\x1b[D",    ConsoleKey.LeftArrow),
        new("\x1b[1;5D", ConsoleKey.LeftArrow,  ctrl:true),

        new("\x1b[H",    ConsoleKey.Home),
        new("\x1b[F",    ConsoleKey.End),

        new("\x1b[5~",   ConsoleKey.PageUp),
        new("\x1b[6~",   ConsoleKey.PageDown),

        // do a better way of mapping this
        new("\x3",       'c',                   ctrl:true),
        new("\x4",       'd',                   ctrl:true),
    };

    InputParseResult ParseControlKey()
    {
        var input = _input.Span;
        var partial = false;

        foreach (var mapping in k_controlMappings)
        {
            var pattern = mapping.Input.AsSpan();
            if (input.StartsWith(pattern))
            {
                _events.Post(new KeyEvent(mapping.Key));
                _inEscape = false;
                _input.Skip(pattern.Length);

                return InputParseResult.Accept;
            }

            if (pattern.StartsWith(input))
                partial = true;
        }

        return partial ? InputParseResult.Partial : InputParseResult.NoMatch;
    }
}
