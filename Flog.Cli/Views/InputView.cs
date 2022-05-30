using System.Text;

class InputView
{
    readonly Screen _screen;

    int _x = 0, _y, _cx, _cursor;
    readonly StringBuilder _command = new();

    public InputView(Screen screen)
    {
        _screen = screen;
    }

    public void Refresh()
    {
        _screen.OutSetCursorPos(0, _y);
        _screen.OutPrint(':');
        _screen.OutPrint(_command.ToString().AsSpan(_x), _cx - 1, true);
        UpdateCursorPos();
    }

    public void SetBounds(int width, int y)
    {
        _cx = width;
        _y = y;

        Refresh();
    }

    void UpdateCursorPos() => _screen.OutSetCursorPos(_cursor - _x + 1, _y);

    void PostUpdatedFilter() => _screen.PostEvent(new FilterUpdatedEvent(_command.ToString()));

    public bool HandleEvent(ITerminalEvent evt)
    {
        switch (evt)
        {
            // TODO: find a readline library to use instead of hacking it like this

            case KeyEvent { Key: ConsoleKey.LeftArrow, NoModifiers: true }:
            case CharEvent { Char: 'b', Alt: false, Ctrl: true }:
                if (_cursor > 0)
                {
                    --_cursor;
                    UpdateCursorPos();
                }
                break;

            case KeyEvent { Key: ConsoleKey.RightArrow, NoModifiers: true }:
            case CharEvent { Char: 'f', Alt: false, Ctrl: true }:
                if (_cursor < _command.Length)
                {
                    ++_cursor;
                    UpdateCursorPos();
                }
                break;

            case KeyEvent { Key: ConsoleKey.Home, NoModifiers: true }:
            case CharEvent { Char: 'a', Alt: false, Ctrl: true }:
                _cursor = 0;
                UpdateCursorPos();
                break;

            case KeyEvent { Key: ConsoleKey.End, NoModifiers: true }:
            case CharEvent { Char: 'e', Alt: false, Ctrl: true }:
                _cursor = _command.Length;
                UpdateCursorPos();
                break;

            case KeyEvent { Key: ConsoleKey.Backspace, NoModifiers: true }:
                if (_cursor > 0)
                {
                    _command.Remove(--_cursor, 1);
                    UpdateCursorPos();
                    _screen.OutDeleteChars(1);
                    PostUpdatedFilter();
                }
                break;

            case KeyEvent { Key: ConsoleKey.Delete, NoModifiers: true }:
            case CharEvent { Char: 'd', Alt: false, Ctrl: true }:
                if (_cursor < _command.Length)
                {
                    _command.Remove(_cursor, 1);
                    _screen.OutDeleteChars(1);
                    PostUpdatedFilter();
                }
                break;

            case CharEvent { Char: 'k', Ctrl: true }:
                if (_cursor < _command.Length)
                {
                    _command.Remove(_cursor, _command.Length - _cursor);
                    _screen.OutClearLine(ClearMode.After);
                    PostUpdatedFilter();
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
                    PostUpdatedFilter();
                }
                break;

            case CharEvent { NoModifiers: true } chrEvt:
                _command.Insert(_cursor++, chrEvt.Char);
                _screen.OutInsertChars(1);
                _screen.OutPrint(chrEvt.Char);
                PostUpdatedFilter();
                break;

            default:
                return false;
        }

        return true;
    }
}
