class TerminalNotInteractiveException : Exception {}
class TerminalInputEofException : Exception {}

interface ITerminalEvent {}

readonly struct ErrorEvent : ITerminalEvent
{
    public readonly Exception Exception;

    public ErrorEvent(Exception exception) => Exception = exception;
}

readonly struct SignalEvent : ITerminalEvent
{
    public readonly TerminalSignal Signal;

    public SignalEvent(TerminalSignal signal)
    {
        Signal = signal;
    }
}

readonly struct KeyEvent : ITerminalEvent
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

readonly struct CharEvent : ITerminalEvent
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

readonly struct ResizeEvent : ITerminalEvent
{
    public readonly TerminalSize NewSize;

    public ResizeEvent(TerminalSize newSize)
    {
        NewSize = newSize;
    }
}

readonly struct FilterUpdatedEvent : ITerminalEvent
{
    public readonly string NewFilter;

    public FilterUpdatedEvent(string newFilter)
    {
        NewFilter = newFilter;
    }
}
