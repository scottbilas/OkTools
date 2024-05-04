using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using Vezel.Cathode.IO;
using static Vezel.Cathode.Text.Control.ControlConstants;

namespace OkTools.Core.Terminal;

// i can get rid of most of this class once this is implemented:
// https://github.com/vezel-dev/cathode/issues/59 "Implement a control sequence parser"

[PublicAPI]
public static class AnsiInput
{
    // TODO: what about ConfigureAwait(false) in here?

    static readonly TimeSpan k_standaloneEscTimeoutMs = TimeSpan.FromMilliseconds(50); // tcell uses this timeout

    public static async IAsyncEnumerable<KeyEvent> SelectReadKeysAsync(TerminalReader rawInput, [EnumeratorCancellation] CancellationToken cancel = default)
    {
        Task? rawReader = null;
        var rawCancelSource = new CancellationTokenSource();
        var rawCancel = rawCancelSource.Token;

        try
        {
            // we need an independently running task for reading raw input, because of an apparent windows bug doing
            // read+timeout (which we are doing on the pipe in another task).
            // see https://github.com/vezel-dev/cathode/issues/165#issuecomment-2085666036.

            var rawInputPipe = new Pipe();
            rawReader = Task.Run(async () =>
            {
                var buffer = new byte[100];
                while (!rawCancel.IsCancellationRequested)
                {
                    var read = await rawInput.ReadPartialAsync(buffer, rawCancel);
                    if (read == 0)
                        break;

                    await rawInputPipe.Writer.WriteAsync(buffer.AsMemory(0, read), rawCancel);
                    await rawInputPipe.Writer.FlushAsync(rawCancel);
                }
            }, cancel);

            await foreach (var item in SelectReadKeysAsync(rawInputPipe.Reader, cancel))
                yield return item;
        }
        finally
        {
            if (rawReader != null)
            {
                try
                {
                    await rawCancelSource.CancelAsync();
                    await rawReader;
                }
                catch (OperationCanceledException) {}
            }
        }
    }

    static async IAsyncEnumerable<KeyEvent> SelectReadKeysAsync(PipeReader inPipe, [EnumeratorCancellation] CancellationToken cancel = default)
    {
        // main loop: read input, parse into key events and yield them

        var arrayPool = ArrayPool<byte>.Shared;
        var readAtLeast = 1;

        while (!cancel.IsCancellationRequested)
        {
            var timerExpired = false;

            ReadResult read;
            if (readAtLeast > 1)
            {
                // possibilities when we have a timeout:
                //
                //  1. it was user hitting esc ("standalone esc")
                //  2. it's an unrecognized esc sequence (need to add cases to the matcher)
                //  3. the sequence didn't completely fit in a packet, and the continuing packet arrives too late

                var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
                timeout.CancelAfter(k_standaloneEscTimeoutMs);
                try
                {
                    read = await inPipe.ReadAtLeastAsync(readAtLeast + 1, timeout.Token);
                }
                catch (OperationCanceledException x) when (x.CancellationToken == timeout.Token)
                {
                    timerExpired = true;
                    inPipe.TryRead(out read); // ok take whatever we have
                }
            }
            else
            {
                read = await inPipe.ReadAsync(cancel);
            }

            // flatten into an array, which we iterate through with an ArraySegment (spans not allowed in async)
            var inputBuffer = arrayPool.Rent((int)read.Buffer.Length);
            read.Buffer.CopyTo(inputBuffer);
            var inputSegment = new ArraySegment<byte>(inputBuffer, 0, (int)read.Buffer.Length);

            while (!inputSegment.IsEmpty())
            {
                if (cancel.IsCancellationRequested)
                    yield break;

                var keyEvent = TryParseKey(ref inputSegment, timerExpired);
                if (keyEvent == null)
                    break;

                yield return keyEvent.Value;
            }

            // if we didn't consume all the data, we'll need to request more next time, or it will just return the same thing
            readAtLeast = inputSegment.Count + 1;

            inPipe.AdvanceTo(read.Buffer.GetPosition(inputSegment.Offset));
            arrayPool.Return(inputBuffer);
        }
    }

    static KeyEvent? TryParseKey(ref ArraySegment<byte> input, bool timerExpired)
    {
        static void Skip(ref ArraySegment<byte> input)
        {
            input = input[1..];
        }

        static byte Read(ref ArraySegment<byte> input)
        {
            var b = input[0];
            Skip(ref input);
            return b;
        }

        // pass through anything that isn't part of an esc sequence. this will include printable chars and also utf-8.
        if (input[0] != ESC)
        {
            var b = Read(ref input);
            return IsControlChar(b)
                ? ParseControlChar(b)
                : new KeyEvent((char)b);
        }

        if (input.Count >= k_minExtendedMapping)
        {
            foreach (var mapping in k_extendedMappings)
            {
                var matchLen = CountSame<byte>(mapping.Pattern.AsSpan(), input);
                if (matchLen == mapping.Pattern.Length)
                {
                    input = input[matchLen..];
                    return mapping.Event;
                }
            }
        }

        if (timerExpired)
            return ParseControlChar(Read(ref input));

        // plain esc leave alone for possible timeout
        if (input.Count == 1)
            return null;

        // alt-control/printable chars. this is potentially ambiguous with some escape sequences so this must come after
        // the above check.

        // skip the esc, we're definitely returning something now
        Skip(ref input);

        if (IsControlChar(input[0]) && input[0] != ESC) // don't want held-down esc key to sometimes come through as alt-ESC
            return ParseControlChar(Read(ref input)) with { Alt = true };
        if (IsPrintableChar(input[0]))
            return new KeyEvent((char)Read(ref input), alt: true);

        // ESC followed by more must be a plain ESC (or a sequence we don't recognize, which we'll just pass through)
        return ParseControlChar(ESC);
    }

    static bool IsControlChar(byte b) => b is <= 0x1f or 0x7f;
    static bool IsPrintableChar(byte b) => b is >= 0x20 and <= 0x7e;

    static readonly KeyEvent[] k_controlChars =
    [
        // these are collisions i have noticed. when receiving one, we have to pick how to translate it
        // back to a key. a few other notes:
        //
        // * ctrl-number cannot be detected at all given it is always overloaded.
        // * shift-ctrl-letter cannot be detected
        // * alt-ctrl-shift-letter same problem as shift-ctrl-letter, we simply get an esc in front of it
        // * alt-letter or alt-shift-letter is fine, it comes through as esc+key (shifted or unshifted)

        /*00*/ default,                                     // collision: ^2 ^` ^-space ^-shift-2
        /*01*/ new(ConsoleKey.A,         'a',  Ctrl: true),
        /*02*/ new(ConsoleKey.B,         'b',  Ctrl: true),
        /*03*/ new(ConsoleKey.C,         'c',  Ctrl: true),
        /*04*/ new(ConsoleKey.D,         'd',  Ctrl: true),
        /*05*/ new(ConsoleKey.E,         'e',  Ctrl: true),
        /*06*/ new(ConsoleKey.F,         'f',  Ctrl: true),
        /*07*/ new(ConsoleKey.G,         'g',  Ctrl: true), // collision: ^'
        /*08*/ new(ConsoleKey.Backspace, BS),               // collision \b: ^H ^8 ^-shift-/ \x7f
        /*09*/ new(ConsoleKey.Tab,       HT),               // collision \t: ^I ^-shift-I
        /*0a*/ new(ConsoleKey.Enter,     LF,   Ctrl: true), // collision \n: ^J ^-shift-J
        /*0b*/ new(ConsoleKey.K,         'k',  Ctrl: true), // collision: \v
        /*0c*/ new(ConsoleKey.L,         'l',  Ctrl: true), // collision: \f
        /*0d*/ new(ConsoleKey.Enter,     CR),               // collision \r: ^M ^-shift-M ^-
        /*0e*/ new(ConsoleKey.N,         'n',  Ctrl: true), // collision: ^.
        /*0f*/ new(ConsoleKey.O,         'o',  Ctrl: true),
        /*10*/ new(ConsoleKey.P,         'p',  Ctrl: true),
        /*11*/ new(ConsoleKey.Q,         'q',  Ctrl: true),
        /*12*/ new(ConsoleKey.R,         'r',  Ctrl: true),
        /*13*/ new(ConsoleKey.S,         's',  Ctrl: true),
        /*14*/ new(ConsoleKey.T,         't',  Ctrl: true),
        /*15*/ new(ConsoleKey.U,         'u',  Ctrl: true),
        /*16*/ new(ConsoleKey.V,         'v',  Ctrl: true),
        /*17*/ new(ConsoleKey.W,         'w',  Ctrl: true), // collision: ^-shift-7
        /*18*/ new(ConsoleKey.X,         'x',  Ctrl: true), // collision: ^-shift-8
        /*19*/ new(ConsoleKey.Y,         'y',  Ctrl: true), // collision: ^-shift-9
        /*1a*/ default,                                     // ctrl-z is "repeat last char", impossible to map
        /*1b*/ new(ConsoleKey.Escape,    ESC),              // collision \x1b: ^[ ^3 ^;
        /*1c*/ new(ConsoleKey.Oem5,      '\\', Ctrl: true), // collision: ^4
        /*1d*/ new(ConsoleKey.Oem6,      ']',  Ctrl: true), // collision: ^5 ^-shift-= ^=
        /*1e*/ new(ConsoleKey.D6,        '^',  Ctrl: true), // collision: ^6 ^-shift-6
        /*1f*/ new(ConsoleKey.OemMinus,  '_',  Ctrl: true), // collision: ^7 ^-shift-- ^/
    ];

    static KeyEvent ParseControlChar(char ch) =>
        ch == '\x7f' ? new(ConsoleKey.Backspace, BS) : k_controlChars[ch];
    static KeyEvent ParseControlChar(byte b) => ParseControlChar((char)b);

    readonly struct KeyMapping
    {
        public readonly byte[] Pattern; // pattern to match input against
        public readonly KeyEvent Event; // what we send out when receiving this

        public KeyMapping(string pattern, KeyEvent evt)
        {
            Pattern = Encoding.ASCII.GetBytes(pattern);
            Event = evt;
        }
    }

    static readonly KeyMapping[] k_extendedMappings =
        [
            // helpful for other similar needs

            // https://github.com/microsoft/terminal/blob/main/src/terminal/input/terminalInput.cpp#L317
            // https://invisible-island.net/xterm/ctlseqs/ctlseqs.pdf
            // https://github.com/gdamore/tcell/blob/main/tscreen.go#L271
            // https://github.com/termbox/termbox/blob/master/termbox.h#L53

            new(CSI+"Z",     ParseControlChar('\t') with { Shift=true }),

            new(CSI+"A",     new(ConsoleKey.UpArrow)),
            new(CSI+"1;5A",  new(ConsoleKey.UpArrow,    ctrl: true)),
            new(CSI+"B",     new(ConsoleKey.DownArrow)),
            new(CSI+"1;5B",  new(ConsoleKey.DownArrow,  ctrl: true)),
            new(CSI+"C",     new(ConsoleKey.RightArrow)),
            new(CSI+"1;5C",  new(ConsoleKey.RightArrow, ctrl: true)),
            new(CSI+"D",     new(ConsoleKey.LeftArrow)),
            new(CSI+"1;5D",  new(ConsoleKey.LeftArrow,  ctrl: true)),

            new(CSI+"1~",    new(ConsoleKey.Home)),
            new(CSI+"1;5H",  new(ConsoleKey.Home, ctrl: true)),
            new(CSI+"4~",    new(ConsoleKey.End)),
            new(CSI+"1;5F",  new(ConsoleKey.End, ctrl: true)),

            new(CSI+"5~",    new(ConsoleKey.PageUp)),
            new(CSI+"6~",    new(ConsoleKey.PageDown)),

            new(CSI+"2~",    new(ConsoleKey.Insert)),
            new(CSI+"3~",    new(ConsoleKey.Delete)),

            new(ESC+"OP",    new(ConsoleKey.F1)),
            new(ESC+"OQ",    new(ConsoleKey.F2)),
            new(ESC+"OR",    new(ConsoleKey.F3)),
            new(ESC+"OS",    new(ConsoleKey.F4)),
            new(CSI+"15~",   new(ConsoleKey.F5)),
            new(CSI+"15;2~", new(ConsoleKey.F5, shift: true)),
            new(CSI+"17~",   new(ConsoleKey.F6)),
            new(CSI+"17;2~", new(ConsoleKey.F6, shift: true)),
            new(CSI+"18~",   new(ConsoleKey.F7)),
            new(CSI+"18;2~", new(ConsoleKey.F7, shift: true)),
            new(CSI+"19~",   new(ConsoleKey.F8)),
            new(CSI+"19;2~", new(ConsoleKey.F8, shift: true)), // TODO: use this pattern for F8 to implement a more general CSI parser (using something like SimpleParser class)
            new(CSI+"19;3~", new(ConsoleKey.F8, alt: true)),
            new(CSI+"19;4~", new(ConsoleKey.F8, alt: true, shift: true)),
            new(CSI+"19;5~", new(ConsoleKey.F8, ctrl: true)),
            new(CSI+"19;6~", new(ConsoleKey.F8, ctrl: true, shift: true)),
            new(CSI+"19;7~", new(ConsoleKey.F8, ctrl: true, alt: true)),
            new(CSI+"19;8~", new(ConsoleKey.F8, ctrl: true, alt: true, shift: true)),
            new(CSI+"20~",   new(ConsoleKey.F9)),
            new(CSI+"20;2~", new(ConsoleKey.F9, shift: true)),
            new(CSI+"21~",   new(ConsoleKey.F10)),
            new(CSI+"21;2~", new(ConsoleKey.F10, shift: true)),
            new(CSI+"23~",   new(ConsoleKey.F11)),
            new(CSI+"23;2~", new(ConsoleKey.F11, shift: true)),
            new(CSI+"24~",   new(ConsoleKey.F12)),
            new(CSI+"24;2~", new(ConsoleKey.F12, shift: true)),
        ];

    static readonly int k_minExtendedMapping = k_extendedMappings.Min(m => m.Pattern.Length);

    static int CountSame<T>(ReadOnlySpan<T> span1, ReadOnlySpan<T> span2) where T : IEquatable<T>
    {
        var count = 0;
        while (count < span1.Length && count < span2.Length && span1[count].Equals(span2[count]))
            ++count;

        return count;
    }
}
