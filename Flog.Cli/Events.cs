class TerminalNotInteractiveException : Exception {}
class TerminalInputEofException : Exception {}

interface IEvent {}

readonly struct SignalEvent : IEvent
{
    public readonly TerminalSignal Signal;

    public SignalEvent(TerminalSignal signal)
    {
        Signal = signal;
    }
}

readonly struct KeyEvent : IEvent
{
    public readonly ConsoleKey Key;
    public readonly bool Shift, Alt, Ctrl;

    public KeyEvent(ConsoleKey key, bool shift = false, bool alt = false, bool ctrl = false)
    {
        Key = key;
        Shift = shift;
        Alt = alt;
        Ctrl = ctrl;
    }

    public bool NoModifiers => !Shift && !Alt && !Ctrl;
}

readonly struct CharEvent : IEvent
{
    public readonly char Char;
    public readonly bool Alt, Ctrl;

    public CharEvent(char chr, bool alt, bool ctrl)
    {
        Char = chr;
        Alt = alt;
        Ctrl = ctrl;
    }

    public bool NoModifiers => !Alt && !Ctrl;
}

readonly struct ResizeEvent : IEvent
{
    public readonly TerminalSize NewSize;

    public ResizeEvent(TerminalSize newSize)
    {
        NewSize = newSize;
    }
}
