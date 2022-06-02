using System.Text;
using System.Threading.Channels;

enum InputParseResult { Accept, NoMatch, Partial }

class InputParser
{
    static readonly TimeSpan k_standaloneEscTimeoutMs = TimeSpan.FromMilliseconds(50); // tcell uses this timeout

    readonly ChannelReader<ReadOnlyMemory<byte>> _rawInput;
    readonly ChannelWriter<ITerminalEvent> _events;

    readonly ByteStream _input = new();
    bool _inEscape;
    DateTime _keyExpire;

    public InputParser(ChannelReader<ReadOnlyMemory<byte>> rawInput, ChannelWriter<ITerminalEvent> events)
    {
        _rawInput = rawInput;
        _events = events;
    }

    public async Task Process()
    {
        var oldSize = _input.Count;

        // TODO: would be better to figure out how to Receive() with a timeout
        // TODO: use timeout on Receive() rather than Receive vs TryReceive vs Sleep etc., to simplify all this

        // block until we get something, unless we're in a partial sequence (in which case sleep to avoid spinning for the full timeout)
        if (_input.Count == 0 && !_inEscape)
            _input.Write(await _rawInput.ReadAsync());
        else
            Thread.Sleep(1);

        // read whatever more is available
        while (_rawInput.TryRead(out var chunk))
            _input.Write(chunk);

        // only consider timer expired if we still haven't gotten any new input
        if (_input.Count != oldSize)
            Parse(false);
        else if (DateTime.UtcNow >= _keyExpire)
            Parse(true);
    }

    async void Parse(bool timerExpired)
    {
        for (;;)
        {
            if (_input.IsEmpty)
            {
                _input.Reset(); // we've consumed all input, so reuse the memory for further writes
                break;
            }

            var partial = false;

            switch (await ParseChar())
            {
                case InputParseResult.Accept: continue;
                case InputParseResult.Partial: partial = true; break;
            }

            switch (await ParseControlKey())
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
                await _events.WriteAsync(new CharEvent((char)b, _inEscape, false));
                _inEscape = false;
            }
            else if (_input.IsEmpty)
            {
                // this is a real esc keypress (we likely got here because ESC+timeout)
                // note that (at least on windows) no modifiers will come through, as the OS uses modified ESC for other things
                await _events.WriteAsync(new KeyEvent(ConsoleKey.Escape));
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

    async Task<InputParseResult> ParseChar()
    {
        var b = _input.Peek();
        if (b >= ' ' && b < 0x7F)
        {
            // normal ascii

            await _events.WriteAsync(new CharEvent((char)b, _inEscape, false));
            _inEscape = false;
            _input.Seek();

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
        public readonly ITerminalEvent Event;

        public ControlMapping(string input, ConsoleKey key, bool shift = false, bool alt = false, bool ctrl = false)
        {
            Input = Encoding.ASCII.GetBytes(input);
            Event = new KeyEvent(key, shift, alt, ctrl);
        }

        public ControlMapping(string input, char ch, bool alt = false, bool ctrl = false)
        {
            Input = Encoding.ASCII.GetBytes(input);
            Event = new CharEvent(ch, alt, ctrl);
        }
    }

    static readonly ControlMapping[] k_controlMappings =
    {
        new("\x1b[A",    ConsoleKey.UpArrow),
        new("\x1b[1;5A", ConsoleKey.UpArrow,    ctrl: true),
        new("\x1b[B",    ConsoleKey.DownArrow),
        new("\x1b[1;5B", ConsoleKey.DownArrow,  ctrl: true),
        new("\x1b[C",    ConsoleKey.RightArrow),
        new("\x1b[1;5C", ConsoleKey.RightArrow, ctrl: true),
        new("\x1b[D",    ConsoleKey.LeftArrow),
        new("\x1b[1;5D", ConsoleKey.LeftArrow,  ctrl: true),

        new("\x1b[H",    ConsoleKey.Home),
        new("\x1b[F",    ConsoleKey.End),

        new("\x1b[2~",   ConsoleKey.Insert),
        new("\x1b[3~",   ConsoleKey.Delete),

        new("\x1b[5~",   ConsoleKey.PageUp),
        new("\x1b[6~",   ConsoleKey.PageDown),

        new("\x7f",      ConsoleKey.Backspace),
        new("\r",        ConsoleKey.Enter),

        new("\x1bOP",    ConsoleKey.F1),
        new("\x1bOQ",    ConsoleKey.F2),
        new("\x1bOR",    ConsoleKey.F3),
        new("\x1bOS",    ConsoleKey.F4),
        new("\x1b[15~",  ConsoleKey.F5),
        new("\x1b[17~",  ConsoleKey.F6),
        new("\x1b[18~",  ConsoleKey.F7),
        new("\x1b[19~",  ConsoleKey.F8),
        new("\x1b[20~",  ConsoleKey.F9),
        new("\x1b[21~",  ConsoleKey.F10),
        new("\x1b[23~",  ConsoleKey.F11),
        new("\x1b[24~",  ConsoleKey.F12),

        // TODO: do a better way of mapping this
        // note that ^M == \r == Enter key..could be we really do want Console+CharKey unified..or maybe ctrl-keys are
        // *always* a ConsoleKey because they're ctrl sequences. also less confusing when trying to figure out what to
        // match against on the receiving side (don't have to look up what ^J or ^M or \r means).
        new("\x1",       'a',                   ctrl: true),
        new("\x2",       'b',                   ctrl: true),
        new("\x3",       'c',                   ctrl: true),
        new("\x4",       'd',                   ctrl: true),
        new("\x5",       'e',                   ctrl: true),
        new("\x6",       'f',                   ctrl: true),
        new("\xb",       'k',                   ctrl: true),
        new("\xc",       'l',                   ctrl: true),
        new("\x14",      't',                   ctrl: true),
        new("\x15",      'u',                   ctrl: true),
    };

    async Task<InputParseResult> ParseControlKey()
    {
        var partial = false;

        foreach (var mapping in k_controlMappings)
        {
            var pattern = mapping.Input.AsMemory();
            if (_input.Span.StartsWith(pattern.Span))
            {
                await _events.WriteAsync(mapping.Event);
                _inEscape = false;
                _input.Seek(pattern.Length);

                return InputParseResult.Accept;
            }

            if (pattern.Span.StartsWith(_input.Span))
                partial = true;
        }

        return partial ? InputParseResult.Partial : InputParseResult.NoMatch;
    }
}
