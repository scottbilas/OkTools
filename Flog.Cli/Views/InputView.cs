using System.Text;

class InputView : ViewBase
{
    readonly Screen _screen;

    int _scrollX, _cursor;
    readonly StringBuilder _command = new();

    public InputView(Screen screen) : base(screen)
    {
        _screen = screen;

        _scrollX = 0;
    }

    public string Prompt { get; set; } = "";

    public string Text
    {
        get => _command.ToString();
        set
        {
            _command.Clear();
            _command.Append(value);
            if (_cursor > _command.Length)
                _cursor = _command.Length;
        }
    }

    public override void SetBounds(int width, int top, int bottom)
    {
        base.SetBounds(width, top, bottom);

        // TODO: adjust _scrollX to keep cursor in view
    }

    public void Accept()
    {
        Text = "";
        // TODO future: add entry to history
    }

    public void Draw()
    {
        CheckEnabled();

        _screen.OutSetCursorPos(0, Top);
        if (_scrollX == 0)
            _screen.OutPrint(Prompt);
        _screen.OutPrint(_command.ToString().AsSpan(_scrollX), Width - Prompt.Length, true);
    }

    public void UpdateCursorPos()
    {
        CheckEnabled();

        _screen.OutSetCursorPos(_cursor - _scrollX + Prompt.Length, Top);
    }

    public (bool accepted, bool inputChanged) HandleEvent(ITerminalEvent evt)
    {
        CheckEnabled();

        var inputChanged = false;

        switch (evt)
        {
            // TODO: find a readline library to use instead of hacking it like this

            // non-modifying cursor movement

            case KeyEvent { Key: ConsoleKey.LeftArrow, NoModifiers: true }:
            case CharEvent { Char: 'b', Alt: false, Ctrl: true }:
                if (_cursor > 0)
                    --_cursor;
                break;

            case KeyEvent { Key: ConsoleKey.RightArrow, NoModifiers: true }:
            case CharEvent { Char: 'f', Alt: false, Ctrl: true }:
                if (_cursor < _command.Length)
                    ++_cursor;
                break;

            case KeyEvent { Key: ConsoleKey.Home, NoModifiers: true }:
            case CharEvent { Char: 'a', Alt: false, Ctrl: true }:
                _cursor = 0;
                break;

            case KeyEvent { Key: ConsoleKey.End, NoModifiers: true }:
            case CharEvent { Char: 'e', Alt: false, Ctrl: true }:
                _cursor = _command.Length;
                break;

            // these change the input (and cursor)

            case KeyEvent { Key: ConsoleKey.Backspace, NoModifiers: true }:
                if (_cursor > 0)
                {
                    _command.Remove(--_cursor, 1);
                    _screen.OutMoveCursorLeft();
                    _screen.OutDeleteChars(1);
                    inputChanged = true;
                }
                break;

            case KeyEvent { Key: ConsoleKey.Delete, NoModifiers: true }:
            case CharEvent { Char: 'd', Alt: false, Ctrl: true }:
                if (_cursor < _command.Length)
                {
                    _command.Remove(_cursor, 1);
                    _screen.OutDeleteChars(1);
                    inputChanged = true;
                }
                break;

            case CharEvent { Char: 'k', Ctrl: true }:
                if (_cursor < _command.Length)
                {
                    _command.Remove(_cursor, _command.Length - _cursor);
                    _screen.OutClearLine(ClearMode.After);
                    inputChanged = true;
                }
                break;

            case CharEvent { Char: 'u', Ctrl: true }:
                if (_cursor > 0)
                {
                    var removed = _cursor;
                    _command.Remove(0, _cursor);
                    _cursor = 0;
                    UpdateCursorPos();
                    _screen.OutDeleteChars(removed);
                    inputChanged = true;
                }
                break;

            case CharEvent { NoModifiers: true } chrEvt:
                _command.Insert(_cursor++, chrEvt.Char);
                _screen.OutInsertChars(1);
                _screen.OutPrint(chrEvt.Char);
                inputChanged = true;
                break;

            default:
                return (false, false);
        }

        return (true, inputChanged);
    }
}
