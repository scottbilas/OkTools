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
        // https://invisible-island.net/xterm/ctlseqs/ctlseqs.pdf

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

        new("\x1b[15;2~",  ConsoleKey.F5, shift: true),
        new("\x1b[17;2~",  ConsoleKey.F6, shift: true),
        new("\x1b[18;2~",  ConsoleKey.F7, shift: true),
        new("\x1b[19;2~",  ConsoleKey.F8, shift: true),
        new("\x1b[20;2~",  ConsoleKey.F9, shift: true),
        new("\x1b[21;2~",  ConsoleKey.F10, shift: true),
        new("\x1b[23;2~",  ConsoleKey.F11, shift: true),
        new("\x1b[24;2~",  ConsoleKey.F12, shift: true),

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
//        .Concat()
//        .ToArray();

    async Task<InputParseResult> ParseControlKey()
    {
        var partial = false;

        // TODO: see prepareKeyModXTerm from tcell\tscreen.go

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

    //Dictionary<Key, bool> _keyExist;
    //Dictionary<string, tKey> _keyCode;

    #if false
func (t *tScreen) prepareKeyMod(key Key, mod ModMask, val string) {
	if val != "" {
		// Do not override codes that already exist
		if _, exist := t.keycodes[val]; !exist {
			t.keyexist[key] = true
			t.keycodes[val] = &tKeyCode{key: key, mod: mod}
		}
	}
}

func (t *tScreen) prepareKeyModReplace(key Key, replace Key, mod ModMask, val string) {
	if val != "" {
		// Do not override codes that already exist
		if old, exist := t.keycodes[val]; !exist || old.key == replace {
			t.keyexist[key] = true
			t.keycodes[val] = &tKeyCode{key: key, mod: mod}
		}
	}
}

func (t *tScreen) prepareKeyModXTerm(key Key, val string) {

	if strings.HasPrefix(val, "\x1b[") && strings.HasSuffix(val, "~") {

		// Drop the trailing ~
		val = val[:len(val)-1]

		// These suffixes are calculated assuming Xterm style modifier suffixes.
		// Please see https://invisible-island.net/xterm/ctlseqs/ctlseqs.pdf for
		// more information (specifically "PC-Style Function Keys").
		t.prepareKeyModReplace(key, key+12, ModShift, val+";2~")
		t.prepareKeyModReplace(key, key+48, ModAlt, val+";3~")
		t.prepareKeyModReplace(key, key+60, ModAlt|ModShift, val+";4~")
		t.prepareKeyModReplace(key, key+24, ModCtrl, val+";5~")
		t.prepareKeyModReplace(key, key+36, ModCtrl|ModShift, val+";6~")
		t.prepareKeyMod(key, ModAlt|ModCtrl, val+";7~")
		t.prepareKeyMod(key, ModShift|ModAlt|ModCtrl, val+";8~")
		t.prepareKeyMod(key, ModMeta, val+";9~")
		t.prepareKeyMod(key, ModMeta|ModShift, val+";10~")
		t.prepareKeyMod(key, ModMeta|ModAlt, val+";11~")
		t.prepareKeyMod(key, ModMeta|ModAlt|ModShift, val+";12~")
		t.prepareKeyMod(key, ModMeta|ModCtrl, val+";13~")
		t.prepareKeyMod(key, ModMeta|ModCtrl|ModShift, val+";14~")
		t.prepareKeyMod(key, ModMeta|ModCtrl|ModAlt, val+";15~")
		t.prepareKeyMod(key, ModMeta|ModCtrl|ModAlt|ModShift, val+";16~")
	} else if strings.HasPrefix(val, "\x1bO") && len(val) == 3 {
		val = val[2:]
		t.prepareKeyModReplace(key, key+12, ModShift, "\x1b[1;2"+val)
		t.prepareKeyModReplace(key, key+48, ModAlt, "\x1b[1;3"+val)
		t.prepareKeyModReplace(key, key+24, ModCtrl, "\x1b[1;5"+val)
		t.prepareKeyModReplace(key, key+36, ModCtrl|ModShift, "\x1b[1;6"+val)
		t.prepareKeyModReplace(key, key+60, ModAlt|ModShift, "\x1b[1;4"+val)
		t.prepareKeyMod(key, ModAlt|ModCtrl, "\x1b[1;7"+val)
		t.prepareKeyMod(key, ModShift|ModAlt|ModCtrl, "\x1b[1;8"+val)
		t.prepareKeyMod(key, ModMeta, "\x1b[1;9"+val)
		t.prepareKeyMod(key, ModMeta|ModShift, "\x1b[1;10"+val)
		t.prepareKeyMod(key, ModMeta|ModAlt, "\x1b[1;11"+val)
		t.prepareKeyMod(key, ModMeta|ModAlt|ModShift, "\x1b[1;12"+val)
		t.prepareKeyMod(key, ModMeta|ModCtrl, "\x1b[1;13"+val)
		t.prepareKeyMod(key, ModMeta|ModCtrl|ModShift, "\x1b[1;14"+val)
		t.prepareKeyMod(key, ModMeta|ModCtrl|ModAlt, "\x1b[1;15"+val)
		t.prepareKeyMod(key, ModMeta|ModCtrl|ModAlt|ModShift, "\x1b[1;16"+val)
	}
}

func (t *tScreen) prepareXtermModifiers() {
	if t.ti.Modifiers != terminfo.ModifiersXTerm {
		return
	}
	t.prepareKeyModXTerm(KeyRight, t.ti.KeyRight)
	t.prepareKeyModXTerm(KeyLeft, t.ti.KeyLeft)
	t.prepareKeyModXTerm(KeyUp, t.ti.KeyUp)
	t.prepareKeyModXTerm(KeyDown, t.ti.KeyDown)
	t.prepareKeyModXTerm(KeyInsert, t.ti.KeyInsert)
	t.prepareKeyModXTerm(KeyDelete, t.ti.KeyDelete)
	t.prepareKeyModXTerm(KeyPgUp, t.ti.KeyPgUp)
	t.prepareKeyModXTerm(KeyPgDn, t.ti.KeyPgDn)
	t.prepareKeyModXTerm(KeyHome, t.ti.KeyHome)
	t.prepareKeyModXTerm(KeyEnd, t.ti.KeyEnd)
	t.prepareKeyModXTerm(KeyF1, t.ti.KeyF1)
	t.prepareKeyModXTerm(KeyF2, t.ti.KeyF2)
	t.prepareKeyModXTerm(KeyF3, t.ti.KeyF3)
	t.prepareKeyModXTerm(KeyF4, t.ti.KeyF4)
	t.prepareKeyModXTerm(KeyF5, t.ti.KeyF5)
	t.prepareKeyModXTerm(KeyF6, t.ti.KeyF6)
	t.prepareKeyModXTerm(KeyF7, t.ti.KeyF7)
	t.prepareKeyModXTerm(KeyF8, t.ti.KeyF8)
	t.prepareKeyModXTerm(KeyF9, t.ti.KeyF9)
	t.prepareKeyModXTerm(KeyF10, t.ti.KeyF10)
	t.prepareKeyModXTerm(KeyF11, t.ti.KeyF11)
	t.prepareKeyModXTerm(KeyF12, t.ti.KeyF12)
}
#endif

}
