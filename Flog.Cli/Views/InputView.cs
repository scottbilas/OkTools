using System.Text;

class InputView : ViewBase
{
    readonly Screen _screen;

    int _scrollX, _cursor;
    readonly StringBuilder _command = new();

    public InputView(Screen screen) : base(screen)
    {
        _screen = screen;
    }

    public void Refresh()
    {
        _screen.OutSetCursorPos(0, Top);
        if (_scrollX == 0)
            _screen.OutPrint(':');
        _screen.OutPrint(_command.ToString().AsSpan(_scrollX), Width - 1, true);
    }

    public override void SetBounds(int width, int top, int bottom)
    {
        base.SetBounds(width, top, bottom);
        Refresh();
    }

    public void UpdateCursor()
    {
        UpdateCursorPos();
        _screen.OutShowCursor(true);
    }

    void UpdateCursorPos()
    {
        _screen.OutSetCursorPos(_cursor - _scrollX + 1, Top);
    }

    void PostUpdatedFilter()
    {
        _screen.PostEvent(new FilterUpdatedEvent(_command.ToString()));
    }

    public bool HandleEvent(ITerminalEvent evt)
    {
        switch (evt)
        {
            // TODO: find a readline library to use instead of hacking it like this

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

            case KeyEvent { Key: ConsoleKey.Backspace, NoModifiers: true }:
                if (_cursor > 0)
                {
                    _command.Remove(--_cursor, 1);
                    _screen.OutMoveCursorLeft();
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

            case KeyEvent { Key: ConsoleKey.Enter }:
                _screen.PostEvent(new FilterCommittedEvent());
                _command.Clear();
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
