using System.Text;

class PromptView
{
    readonly Screen _screen;

    int _x = 0, _y, _cx, _cursor;
    readonly StringBuilder _command = new();

    public PromptView(Screen screen)
    {
        _screen = screen;
    }

    public void Refresh()
    {
        _screen.OutSetCursorPos(0, _y);
        _screen.OutPrint(':');
        _screen.OutPrint(_command.ToString().AsSpan(_x), _cx - 1, true);
        UpdateCursor();
    }

    public void SetBounds(int width, int y)
    {
        _cx = width;
        _y = y;

        Refresh();
    }

    void UpdateCursor() => _screen.OutSetCursorPos(_cursor - _x + 1, _y);

    public void HandleEvent(IEvent evt)
    {
        switch (evt)
        {
            // TODO: find a readline library to use instead of hacking it like this

            case KeyEvent { Key: ConsoleKey.LeftArrow, NoModifiers: true }:
            case CharEvent { Char: 'b', Alt: false, Ctrl: true }:
                if (_cursor > 0)
                {
                    --_cursor;
                    UpdateCursor();
                }
                break;

            case KeyEvent { Key: ConsoleKey.RightArrow, NoModifiers: true }:
            case CharEvent { Char: 'f', Alt: false, Ctrl: true }:
                if (_cursor < _command.Length)
                {
                    ++_cursor;
                    UpdateCursor();
                }
                break;

            case KeyEvent { Key: ConsoleKey.Home, NoModifiers: true }:
            case CharEvent { Char: 'a', Alt: false, Ctrl: true }:
                _cursor = 0;
                UpdateCursor();
                break;

            case KeyEvent { Key: ConsoleKey.End, NoModifiers: true }:
            case CharEvent { Char: 'e', Alt: false, Ctrl: true }:
                _cursor = _command.Length;
                UpdateCursor();
                break;

            case KeyEvent { Key: ConsoleKey.Backspace, NoModifiers: true }:
                if (_cursor > 0)
                {
                    _command.Remove(--_cursor, 1);
                    UpdateCursor();
                    _screen.OutDeleteChars(1);
                }
                break;

            case KeyEvent { Key: ConsoleKey.Delete, NoModifiers: true }:
            case CharEvent { Char: 'd', Alt: false, Ctrl: true }:
                if (_cursor < _command.Length)
                {
                    _command.Remove(_cursor, 1);
                    _screen.OutDeleteChars(1);
                }
                break;

            case CharEvent { Char: 'k', Ctrl: true }:
                if (_cursor < _command.Length)
                {
                    _command.Remove(_cursor, _command.Length - _cursor);
                    _screen.OutClearLine(ClearMode.After);
                }
                break;

            case CharEvent { Char: 'u', Ctrl: true }:
                if (_cursor > 0)
                {
                    var removed = _cursor;
                    _command.Remove(0, _cursor);
                    _cursor = 0;
                    UpdateCursor();
                    _screen.OutDeleteChars(removed);
                }
                break;

            case CharEvent { NoModifiers: true } chrEvt:
                _command.Insert(_cursor++, chrEvt.Char);
                _screen.OutInsertChars(1);
                _screen.OutPrint(chrEvt.Char);
                break;
        }
    }
}
