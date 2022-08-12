using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using OkTools.Flog;

enum WrapType
{
    None,
    Rigid,
    Word,
}

class ScrollingTextView : ViewBase
{
    // source lines
    readonly ILineDataSource _source;       // raw lines
    int _sourceLineCount;                   // detect if the source has new lines available
    uint _sourceVersion;                    // detect if raw source totally changes (e.g. file truncated)
    // visible region
    DisplayLine[] _displayLines;            // the screen.height set of actual lines we're showing
    readonly BitArray _displayLinesValid;   // mutable state of each display line

    readonly StringBuilder _sb = new();
    int _scrollX, _scrollY; // TODO: _scrollSubY;

    WrapType _wrapType;

    readonly record struct DisplayLine(int LineIndex, string? Chars, int Begin, int End)
    {
        public int Length => End - Begin;

        public ReadOnlySpan<char> Span => Chars.AsSpan(Begin, Length);
    }

    public ScrollingTextView(Screen screen, ILineDataSource source) : base(screen)
    {
        _source = source;
        _sourceVersion = source.Version - 1;
        _displayLines = new DisplayLine[screen.Size.Height];
        _displayLinesValid = new BitArray(screen.Size.Height);
    }

    public ILineDataSource Processor => _source;
    public bool IsFollowing { get; set; }

    void Invalidate()
    {
        _displayLinesValid.SetAll(false);

        #if DEBUG
        Array.Fill(_displayLines, default);
        #endif
    }

    void Invalidate(int begin, int end)
    {
        for (var i = begin; i != end; ++i)
            _displayLinesValid[i] = false;

        #if DEBUG
        Array.Fill(_displayLines, default, begin, end - begin);
        #endif
    }

    public WrapType WrapType
    {
        get => _wrapType;

        set
        {
            if (_wrapType == value)
                return;

            _wrapType = value;
            Invalidate();
        }
    }

    public void Update()
    {
        CheckEnabled();

        // react to changes in source

        if (_sourceVersion != _source.Version)
        {
            // version change means something drastic happened at the source, so invalidate everything

            _sourceVersion = _source.Version;
            _sourceLineCount = 0;

            Invalidate();
        }
        else if (_sourceLineCount != _source.Count)
        {
            // new lines available from source, so invalidate any display lines that previously resolved to "past EOF"

            for (var i = 0; i < _displayLines.Length; ++i)
            {
                if (_displayLines[i].Chars == null)
                {
                    Invalidate(i, _displayLines.Length);
                    break;
                }
            }
        }

        // finish reacting to changes in source

        if (_sourceLineCount != _source.Count)
        {
            _sourceLineCount = _source.Count;

            if (IsFollowing)
                ScrollToBottom(false);
        }

        // validate and draw

        for (var i = 0; i < _displayLinesValid.Length; ++i)
        {
            if (_displayLinesValid[i])
                continue;

            var index = _scrollY + i;
            if (index < _sourceLineCount)
            {
                var line = GetProcessedLine(index);
                _displayLines[i] = new DisplayLine(index, line, 0, line.Length);
            }
            else
                _displayLines[i] = default;

            Screen.OutSetCursorPos(0, i);

            if (_displayLines[i].Chars != null)
            {
                var start = _wrapType == WrapType.None ? _scrollX : 0;
                var span = _displayLines[i].Span.SliceSafe(start);

                Screen.OutPrint(span, Width, true);
            }
            else
            {
                // TODO: make this char optional
                // TODO: render in dark gray

                Screen.OutPrint("~", Width, true);
            }

            _displayLinesValid[i] = true;
        }
    }

    public int ScrollPos => _scrollY;

    public void ScrollDown() => ScrollY(-1);
    public void ScrollUp() => ScrollY(1);
    public void ScrollHalfPageDown() => ScrollY(-Height / 2);
    public void ScrollHalfPageUp() => ScrollY(Height / 2);
    public void ScrollPageDown() => ScrollY(-Height);
    public void ScrollPageUp() => ScrollY(Height);
    public void ScrollLeft() => ScrollX(Screen.Options.HorizScrollSize);
    public void ScrollRight() => ScrollX(-Screen.Options.HorizScrollSize);

    public void ScrollToTop()
    {
        if (!ScrollToY(0))
            ScrollToX(0);
    }

    public void ScrollToBottom(bool toggleHalfway)
    {
        var target = ClampY(_sourceLineCount - Height);
        if (target == _scrollY && toggleHalfway && _sourceLineCount >= Height)
            target = ClampY(_sourceLineCount - Height / 2);
        else
            IsFollowing = true;

        ScrollToY(target, IsUserAction.No);
    }

    public override void SetBounds(int width, int top, int bottom)
    {
        // TODO: if no wrap, we don't need full redraw, just fill/delete chars

        var oldWidth = Width;
        var oldTop = Top;
        var oldBottom = Bottom;
        var oldScrollY = _scrollY;

        base.SetBounds(width, top, bottom);

        Array.Resize(ref _displayLines, Height);
        _displayLinesValid.Length = Height;

        // TODO: maintain scroll position regardless of origin (minimize distracting text movement)
        //_scrollY = ClampY(oldScrollY + Top - oldTop);
        // TODO: ^ handle wrap
/*
        if (needsFullDraw)
            InvalidateDraw();
        else
        {
            // fill in anything that's new
            if (Top < oldTop)
                InvalidateDraw(0, oldTop - Top);
            if (Bottom > oldBottom)
                InvalidateDraw(Bottom - oldBottom, Height);
        }*/

        Screen.OutSetScrollMargins(Top, Bottom - 1);

        Invalidate();
    }

    protected override void OnEnabledChanged(bool enabled)
    {
        if (enabled)
            Invalidate(); // wrap etc. could have changed while we were away
    }

    int ClampY(int testY) => Math.Clamp(testY, 0, _sourceLineCount - 1);

    public bool ScrollToY(int y) => ScrollToY(y, IsUserAction.Yes);

    enum IsUserAction { No, Yes }

    bool ScrollToY(int y, IsUserAction isUserAction)
    {
        // user-initiated vertical scroll auto-kills follow
        if (isUserAction == IsUserAction.Yes)
            IsFollowing = false;

        y = ClampY(y);
        if (_scrollY == y)
            return false;

        var offset = _scrollY - y;

        if (Math.Abs(offset) < Height)
        {
            // scroll the screen
            if (offset > 0)
                Screen.OutScrollBufferDown(offset);
            else
                Screen.OutScrollBufferUp(-offset);

            // scroll our display lines/state, invalidating newly exposed lines
            _displayLinesValid.Shift(offset);
            _displayLines.Shift(offset
                #if DEBUG
                , default
                #endif
                );
        }
        else
        {
            // at least a screen worth of change, no point in scrolling
            Invalidate();
        }

        _scrollY = y;
        return true;
    }

    void ScrollY(int offset) => ScrollToY(_scrollY + offset);

    public void ScrollToX(int x)
    {
        // TODO: use OutDelete/InsertChars then fill missing chars from new scroll (maybe can share solution with SetBounds(newWidth))
        // (would be nice to be able to do this to avoid the full screen repaint when on a slow ssh connection with large terminal)

        // ignore if in wrap mode
        if (_wrapType != WrapType.None)
            return;

        if (x < 0)
            x = 0;

        if (_scrollX == x)
            return;

        _scrollX = x;
        Invalidate();
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
                ScrollToBottom(true);
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

    string GetProcessedLine(int lineIndex)
    {
        var line = _source.Lines[lineIndex];

        var hasEscapeSeqs = false;
        var hasControlChars = false;

        foreach (var c in line)
        {
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
                    var indent = (_sb.Length + 1) % Screen.Options.TabWidth;
                    _sb.Append(' ', Screen.Options.TabWidth - indent + 1);
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
