using System.Text;
using System.Text.RegularExpressions;
using NiceIO;
using Vezel.Cathode;
using Vezel.Cathode.Text.Control;

class Options
{
    public readonly int TabWidth = 4;
    public readonly int HorizScrollSize = 10;
}

class View
{
    static readonly string k_empty = new(' ', 100);

    readonly VirtualTerminal _terminal;
    readonly Options _options;
    readonly string[] _lines;
    readonly string?[] _processedLines;
    readonly ControlBuilder _cb = new();
    readonly StringBuilder _sb = new();

    int _x, _y, _cx, _cy;

    public View(VirtualTerminal terminal, Options options, NPath path)
    {
        _terminal = terminal;
        _options = options;
        _lines = path.ReadAllLines();
        _processedLines = new string?[_lines.Length];

        _cx = _terminal.Size.Width;
        _cy = _terminal.Size.Height;
    }

    public void Refresh() => Refresh(0, _cy);

    public void ScrollDown() => ScrollY(-1);
    public void ScrollUp() => ScrollY(1);
    public void ScrollHalfPageDown() => ScrollY(-_cy/2);
    public void ScrollHalfPageUp() => ScrollY(_cy/2);
    public void ScrollPageDown() => ScrollY(-_cy);
    public void ScrollPageUp() => ScrollY(_cy);

    public void ScrollLeft() => ScrollX(_options.HorizScrollSize);
    public void ScrollRight() => ScrollX(-_options.HorizScrollSize);

    public void ScrollToTop()
    {
        if (!ScrollToY(0))
            ScrollToX(0);
    }

    public void ScrollToBottom()
    {
        var target = _lines.Length - (_cy / 2);
        ScrollToY(_y < target ? target : _lines.Length);
    }

    public void Resized() // TODO: SetBounds instead
    {
        _cx = _terminal.Size.Width;
        _cy = _terminal.Size.Height;
        _y = ClampY(_y);

        Refresh();
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

    void Refresh(int beginScreenY, int endScreenY)
    {
        var endPrintY = Math.Min(endScreenY, _lines.Length - _y);

        for (var i = beginScreenY; i < endPrintY; ++i)
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

        for (var i = endPrintY; i < endScreenY; ++i)
        {
            _cb.MoveCursorTo(0, i);
            _cb.ClearLine();
        }

        _terminal.Out(_cb);
        _cb.Clear(_cx * _cy * 2);
    }

    int ClampY(int testY)
    {
        return Math.Max(Math.Min(testY, _lines.Length - 1), 0);
    }

    public bool ScrollToY(int y)
    {
        y = ClampY(y);

        var (beginScreenY, endScreenY) = (0, _cy);
        var offset = y - _y;

        switch (offset)
        {
            case > 0 when offset < _cy:
                _cb.MoveBufferUp(offset);
                beginScreenY = _cy - offset;
                break;
            case < 0 when -offset < _cy:
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
