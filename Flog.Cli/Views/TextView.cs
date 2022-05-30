using System.Text;
using System.Text.RegularExpressions;
using OkTools.Core;

class TextView
{
    readonly Screen _screen;
    readonly ILogSource _logSource;
    readonly string?[] _processedLines;
    readonly StringBuilder _sb = new();

    int _x, _y, _cx, _top, _bottom;
    // TODO: bool for "tail on", also need some options for how we refresh

    public TextView(Screen screen, ILogSource logSource)
    {
        _screen = screen;
        _logSource = logSource;
        _processedLines = new string?[logSource.Lines.Count];
    }

    public int Height => _bottom - _top;
    public int Top => _top;
    public int Bottom => _bottom;

    public void ScrollDown() => ScrollY(-1);
    public void ScrollUp() => ScrollY(1);
    public void ScrollHalfPageDown() => ScrollY(-Height/2);
    public void ScrollHalfPageUp() => ScrollY(Height/2);
    public void ScrollPageDown() => ScrollY(-Height);
    public void ScrollPageUp() => ScrollY(Height);

    public void ScrollLeft() => ScrollX(_screen.Options.HorizScrollSize);
    public void ScrollRight() => ScrollX(-_screen.Options.HorizScrollSize);

    public void ScrollToTop()
    {
        if (!ScrollToY(0))
            ScrollToX(0);
    }

    public void ScrollToBottom()
    {
        var end = _logSource.Lines.Count;
        var target = end - (Height / 2);
        ScrollToY(_y < target ? target : end);
    }

    public void SetBounds(int width, int top, int bottom)
    {
        var needsFullRefresh = _cx != width;
        _cx = width;

        var oldTop = _top;
        var oldBottom = _bottom;
        var oldY = _y;

        _top = top;
        _bottom = bottom;

        _screen.OutSetScrollMargins(_top, _bottom - 1);

        // maintain scroll position regardless of origin (minimize distracting text movement)
        _y = ClampY(oldY + _top - oldTop);

        if (needsFullRefresh)
            Refresh();
        else
        {
            // fill in anything that's new
            if (_top < oldTop)
                Refresh(0, oldTop - _top);
            if (_bottom > oldBottom)
                Refresh(_bottom - oldBottom, Height);
        }
    }

    void Refresh() => Refresh(0, Height);

    void Refresh(int top, int bottom)
    {
        var endPrintY = Math.Min(bottom, _logSource.Lines.Count - _y);

        for (var i = top; i < endPrintY; ++i)
        {
            var line = _processedLines[i + _y] ?? (_processedLines[i + _y] = ProcessForDisplay(_logSource.Lines[i + _y]));

            _screen.OutSetCursorPos(0, i);
            _screen.OutPrint(line.AsSpanSafe(_x), _cx, true);
        }

        for (var i = endPrintY; i < bottom; ++i)
        {
            _screen.OutSetCursorPos(0, i);
            _screen.OutClearLine();
        }
    }

    int ClampY(int testY)
    {
        return Math.Max(Math.Min(testY, _logSource.Lines.Count - 1), 0);
    }

    public bool ScrollToY(int y)
    {
        y = ClampY(y);

        var (beginScreenY, endScreenY) = (0, Height);
        var offset = y - _y;

        switch (offset)
        {
            case > 0 when offset < Height:
                _screen.OutScrollBufferUp(offset);
                beginScreenY = Height - offset;
                break;
            case < 0 when -offset < Height:
                _screen.OutScrollBufferDown(-offset);
                endScreenY = -offset;
                break;
            case 0:
                return false;
        }

        _y = y;
        Refresh(beginScreenY, endScreenY);
        return true;
    }

    void ScrollY(int offset) => ScrollToY(_y + offset);

    public void ScrollToX(int x)
    {
        if (x < 0)
            x = 0;

        if (_x == x)
            return;

        _x = x;
        Refresh();
    }

    void ScrollX(int offset) => ScrollToX(_x + offset);

    public bool HandleEvent(ITerminalEvent evt, Action? pre = null, Action? post = null)
    {
        void Preserve(Action action)
        {
            pre?.Invoke();
            action();
            post?.Invoke();
        }

        switch (evt)
        {
            case KeyEvent { Key: ConsoleKey.Home, NoModifiers: true }:
                Preserve(ScrollToTop);
                break;
            case KeyEvent { Key: ConsoleKey.End, NoModifiers: true }:
                Preserve(ScrollToBottom);
                break;

            case KeyEvent { Key: ConsoleKey.UpArrow, NoModifiers: true }:
            case CharEvent { Char: 'k', NoModifiers: true }:
                Preserve(ScrollDown);
                break;
            case KeyEvent { Key: ConsoleKey.DownArrow, NoModifiers: true }:
            case CharEvent { Char: 'j', NoModifiers: true }:
                Preserve(ScrollUp);
                break;

            case CharEvent { Char: 'K', NoModifiers: true }:
                Preserve(ScrollHalfPageDown);
                break;
            case CharEvent { Char: 'J', NoModifiers: true }:
                Preserve(ScrollHalfPageUp);
                break;

            case KeyEvent { Key: ConsoleKey.LeftArrow, NoModifiers: true }:
            case CharEvent { Char: 'h', NoModifiers: true }:
                Preserve(ScrollRight);
                break;
            case KeyEvent { Key: ConsoleKey.RightArrow, NoModifiers: true }:
            case CharEvent { Char: 'l', NoModifiers: true }:
                Preserve(ScrollLeft);
                break;

            case CharEvent { Char: 'H', NoModifiers: true }:
                Preserve(() => ScrollToX(0));
                break;

            case KeyEvent { Key: ConsoleKey.PageUp, NoModifiers: true }:
                Preserve(ScrollPageDown);
                break;
            case KeyEvent { Key: ConsoleKey.PageDown, NoModifiers: true }:
                Preserve(ScrollPageUp);
                break;

            default:
                return false;
        }

        return true;
    }

    // regex from https://github.com/chalk/ansi-regex/blob/main/index.js
    // TODO: eliminate the rx and work on spans by porting fzf's escape parser from https://github.com/junegunn/fzf/blob/master/src/ansi.go#L130
    static readonly Regex s_ansiEscapes = new(
        "[\\u001B\\u009B][[\\]()#;?]*(?:(?:(?:(?:;[-a-zA-Z\\d\\/#&.:=?%@~_]+)*|[a-zA-Z\\d]+(?:;[-a-zA-Z\\d\\/#&.:=?%@~_]*)*)?\\u0007)|" +
        "(?:(?:\\d{1,4}(?:;\\d{0,4})*)?[\\dA-PR-TZcf-nq-uy=><~]))");

    string ProcessForDisplay(string line)
    {
        var hasEscapeSeqs = false;
        var hasControlChars = false;

        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < line.Length; ++i)
        {
            var c = line[i];
            if (c == 0x1b)
            {
                hasEscapeSeqs = true;
                if (hasControlChars)
                    break;
            }
            else if (c <= 0x1f || c == 0x7f)
            {
                hasControlChars = true;
                if (hasEscapeSeqs)
                    break;
            }
        }

        if (hasEscapeSeqs)
            line = s_ansiEscapes.Replace(line, "");

        if (hasControlChars)
        {
            _sb.Clear();
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < line.Length; ++i)
            {
                var c = line[i];
                if (c == '\t' && _screen.Options.TabWidth > 0)
                {
                    var indent = (_sb.Length+1) % _screen.Options.TabWidth;
                    _sb.Append(' ', _screen.Options.TabWidth - indent+1);
                }
                else if (c <= 0x1f || c == 0x7f)
                {
                    // just skip these
                    // TODO: consider insert a special char like from https://unicode-table.com/en/blocks/control-pictures/
                }
                else
                    _sb.Append(c);
            }

            line = _sb.ToString();
        }

        return line;
    }
}
