using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using OkTools.Core;

class ScrollingTextView : ViewBase
{
    readonly LogProcessor _source;
    uint _logSourceVersion;

    readonly OkList<string?> _processedLines = new(LogProcessor.DefaultCapacity);
    readonly StringBuilder _sb = new();

    int _scrollX, _scrollY;
    // TODO: bool for "follow on", also need some options for how often we draw as we follow the tail
    //       ^ consider using VS-output-style following..if go to end, it turns on follow, and if do any scroll at all, it turns it off..

    public ScrollingTextView(Screen screen, LogProcessor source) : base(screen)
    {
        _source = source;
        _logSourceVersion = source.Version - 1;
    }

    public LogProcessor Processor => _source;

    public void Update(bool drawIfChanged)
    {
        if (_logSourceVersion != _source.Version)
        {
            _processedLines.ClearItems();
            _processedLines.Count = _source.Count;
            _logSourceVersion = _source.Version;

            if (drawIfChanged)
                Draw();
        }
        else if (_source.Count != _processedLines.Count)
        {
            var changedStart = _processedLines.Count;
            _processedLines.Count = _source.Count;
            var changedEnd = _processedLines.Count;

            var viewStart = _scrollY;
            var viewEnd = viewStart + Height;

            if (drawIfChanged && changedStart < viewEnd && changedEnd > viewStart)
                Draw(Math.Max(changedStart, viewStart) - _scrollY, Math.Min(changedEnd, viewEnd) - _scrollY);
        }
    }

    public int ScrollPos => _scrollY;

    public void ScrollDown() => ScrollY(-1);
    public void ScrollUp() => ScrollY(1);
    public void ScrollHalfPageDown() => ScrollY(-Height/2);
    public void ScrollHalfPageUp() => ScrollY(Height/2);
    public void ScrollPageDown() => ScrollY(-Height);
    public void ScrollPageUp() => ScrollY(Height);
    public void ScrollLeft() => ScrollX(Screen.Options.HorizScrollSize);
    public void ScrollRight() => ScrollX(-Screen.Options.HorizScrollSize);

    public void ScrollToTop()
    {
        if (!ScrollToY(0))
            ScrollToX(0);
    }

    public void ScrollToBottom()
    {
        var end = _source.Count;
        var target = end - (Height / 2);
        ScrollToY(_scrollY < target ? target : end);
    }

    public override void SetBounds(int width, int top, int bottom)
    {
        SetBounds(width, top, bottom, false);
    }

    public void SetBounds(int width, int top, int bottom, bool forceRedraw)
    {
        var needsFullDraw = forceRedraw || Width < width;
        var oldTop = Top;
        var oldBottom = Bottom;
        var oldScrollY = _scrollY;

        base.SetBounds(width, top, bottom);

        // maintain scroll position regardless of origin (minimize distracting text movement)
        _scrollY = ClampY(oldScrollY + Top - oldTop);

        if (needsFullDraw)
            Draw();
        else
        {
            // fill in anything that's new
            if (Top < oldTop)
                Draw(0, oldTop - Top);
            if (Bottom > oldBottom)
                Draw(Bottom - oldBottom, Height);
        }

        Screen.OutSetScrollMargins(Top, Bottom - 1);
    }

    public void Draw() => Draw(0, Height);

    void Draw(int top, int bottom)
    {
        CheckEnabled();

        Debug.Assert(top >= 0 && top <= Height);
        Debug.Assert(bottom >= top && bottom <= Height);

        var endPrintY = Math.Min(bottom, _source.Count - _scrollY);

        for (var i = top; i < endPrintY; ++i)
        {
            ref var pline = ref _processedLines.RefAt(i + _scrollY);
            var line = pline ??= SanitizeForDisplay(_source.Lines[i + _scrollY]);

            Screen.OutSetCursorPos(0, i);
            Screen.OutPrint(line.AsSpanSafe(_scrollX), Width, true);
        }

        for (var i = endPrintY; i < bottom; ++i)
        {
            Screen.OutSetCursorPos(0, i);
            Screen.OutClearLine();
        }
    }

    int ClampY(int testY)
    {
        return Math.Max(Math.Min(testY, _source.Count - 1), 0);
    }

    public bool ScrollToY(int y)
    {
        y = ClampY(y);

        var (beginScreenY, endScreenY) = (0, Height);
        var offset = y - _scrollY;

        switch (offset)
        {
            case > 0 when offset < Height:
                Screen.OutScrollBufferUp(offset);
                beginScreenY = Height - offset;
                break;
            case < 0 when -offset < Height:
                Screen.OutScrollBufferDown(-offset);
                endScreenY = -offset;
                break;
            case 0:
                return false;
        }

        _scrollY = y;
        Draw(beginScreenY, endScreenY);
        return true;
    }

    void ScrollY(int offset) => ScrollToY(_scrollY + offset);

    public void ScrollToX(int x)
    {
        if (x < 0)
            x = 0;

        if (_scrollX == x)
            return;

        _scrollX = x;
        Draw();
    }

    void ScrollX(int offset) => ScrollToX(_scrollX + offset);

    public bool HandleEvent(ITerminalEvent evt)
    {
        CheckEnabled();

        // TODO: scrolling in a later filter should also (optionally) update base filter scroll pos

        switch (evt)
        {
            case KeyEvent { Key: ConsoleKey.Home, NoModifiers: true }:
                ScrollToTop();
                break;
            case KeyEvent { Key: ConsoleKey.End, NoModifiers: true }:
                ScrollToBottom();
                break;

            case KeyEvent { Key: ConsoleKey.UpArrow, NoModifiers: true }:
            case CharEvent { Char: 'k', NoModifiers: true }:
                ScrollDown();
                break;
            case KeyEvent { Key: ConsoleKey.DownArrow, NoModifiers: true }:
            case CharEvent { Char: 'j', NoModifiers: true }:
                ScrollUp();
                break;

            case CharEvent { Char: 'K', NoModifiers: true }:
                ScrollHalfPageDown();
                break;
            case CharEvent { Char: 'J', NoModifiers: true }:
                ScrollHalfPageUp();
                break;

            case KeyEvent { Key: ConsoleKey.LeftArrow, NoModifiers: true }:
            case CharEvent { Char: 'h', NoModifiers: true }:
                ScrollRight();
                break;
            case KeyEvent { Key: ConsoleKey.RightArrow, NoModifiers: true }:
            case CharEvent { Char: 'l', NoModifiers: true }:
                ScrollLeft();
                break;

            case CharEvent { Char: 'H', NoModifiers: true }:
                ScrollToX(0);
                break;

            case KeyEvent { Key: ConsoleKey.PageUp, NoModifiers: true }:
                ScrollPageDown();
                break;
            case KeyEvent { Key: ConsoleKey.PageDown, NoModifiers: true }:
                ScrollPageUp();
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

    string SanitizeForDisplay(string line)
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
                if (c == '\t' && Screen.Options.TabWidth > 0)
                {
                    var indent = (_sb.Length+1) % Screen.Options.TabWidth;
                    _sb.Append(' ', Screen.Options.TabWidth - indent+1);
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
