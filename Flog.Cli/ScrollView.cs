using System.Text;
using System.Text.RegularExpressions;
using NiceIO;

class Options
{
    public readonly int TabWidth = 4;
    public readonly int HorizScrollSize = 10;
}

class ScrollView
{
    static readonly string k_empty = new(' ', 100);

    readonly Screen _screen;
    readonly Options _options;
    readonly string[] _lines;
    readonly string?[] _processedLines;
    readonly ControlBuilder _cb = new();
    readonly StringBuilder _sb = new();

    int _x, _y, _cx, _top, _bottom;

    public ScrollView(Screen screen, Options options, NPath path)
    {
        _screen = screen;
        _options = options;
        _lines = path.ReadAllLines();
        _processedLines = new string?[_lines.Length];
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

    public void ScrollLeft() => ScrollX(_options.HorizScrollSize);
    public void ScrollRight() => ScrollX(-_options.HorizScrollSize);

    public void ScrollToTop()
    {
        if (!ScrollToY(0))
            ScrollToX(0);
    }

    public void ScrollToBottom()
    {
        var target = _lines.Length - (Height / 2);
        ScrollToY(_y < target ? target : _lines.Length);
    }

    public void SetBounds(int width, int top, int bottom, bool draw = true)
    {
        var needsFullRefresh = _cx != width;
        _cx = width;

        var oldTop = _top;
        var oldBottom = _bottom;
        var oldY = _y;

        _top = top;
        _bottom = bottom;

        _screen.WriteAndClear(_cb.SetScrollMargin(_top, _bottom - 1));

        // maintain scroll position regardless of origin (minimize distracting text movement)
        _y = ClampY(oldY + _top - oldTop);

        if (draw)
        {
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
                if (c == '\t' && _options.TabWidth > 0)
                {
                    var indent = (_sb.Length+1) % _options.TabWidth;
                    _sb.Append(k_empty, 0, _options.TabWidth - indent+1);
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

    void Refresh() => Refresh(0, Height);

    void Refresh(int top, int bottom)
    {
        var endPrintY = Math.Min(bottom, _lines.Length - _y);

        for (var i = top; i < endPrintY; ++i)
        {
            _cb.MoveCursorTo(0, i);

            var line = _processedLines[i + _y] ?? (_processedLines[i + _y] = ProcessForDisplay(_lines[i + _y]));

            var len = Math.Min(line.Length - _x, _cx);
            if (len > 0)
            {
                _cb.Print(line.AsSpan(_x, len));

                for (var remain = _cx - len; remain > 0; )
                {
                    var write = Math.Min(remain, k_empty.Length);
                    _cb.Print(k_empty.AsSpan(0, write));
                    remain -= write;
                }
            }
            else
                _cb.ClearLine();
        }

        for (var i = endPrintY; i < bottom; ++i)
        {
            _cb.MoveCursorTo(0, i);
            _cb.ClearLine();
        }

        _screen.WriteAndClear(_cb, _cx * Height * 2);
    }

    int ClampY(int testY)
    {
        return Math.Max(Math.Min(testY, _lines.Length - 1), 0);
    }

    public bool ScrollToY(int y)
    {
        y = ClampY(y);

        var (beginScreenY, endScreenY) = (0, Height);
        var offset = y - _y;

        switch (offset)
        {
            case > 0 when offset < Height:
                _cb.MoveBufferUp(offset);
                beginScreenY = Height - offset;
                break;
            case < 0 when -offset < Height:
                _cb.MoveBufferDown(-offset);
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
}
