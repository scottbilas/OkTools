using NiceIO;
using OkTools.Core;

class StatusView : ViewBase
{
    char[] _text = Array.Empty<char>();
    bool _changed = true;

    NPath? _logPath;
    int _currentFilterIndex;
    (int current, int total)[] _filterViewStates = Array.Empty<(int, int)>();

    public StatusView(Screen screen) : base(screen) {}

    public override void SetBounds(int width, int top, int bottom)
    {
        base.SetBounds(width, top, bottom);

        if (_text.Length != Width)
        {
            _text = new char[width];
            _changed = true;
        }
    }

    public void SetLogPath(NPath path)
    {
        if (_logPath == path)
            return;

        _logPath = path;
        _changed = true;
    }

    public void SetFilterStatus(FilterChainView filterPane)
    {
        if (_currentFilterIndex != filterPane.CurrentIndex)
        {
            _currentFilterIndex = filterPane.CurrentIndex;
            _changed = true;
        }

        if (_filterViewStates.Length != filterPane.Filters.Count)
        {
            _filterViewStates = new (int, int)[filterPane.Filters.Count];
            _changed = true;
        }

        for (var i = 0; i < _filterViewStates.Length; ++i)
        {
            var state = (filterPane.Filters[i].ScrollPos, filterPane.Filters[i].LogSource.Lines.Count);
            if (_filterViewStates[i] != state)
            {
                _filterViewStates[i] = state;
                _changed = true;
            }
        }
    }

    public void DrawIfChanged()
    {
        CheckEnabled();

        if (_changed)
            Draw();
    }

    public void Draw()
    {
        CheckEnabled();

        var csb = new CharSpanBuilder(_text);

        // TODO: "TryAppend", put in sections with center/left/etc-aligning, truncate, deal with pathological widths, etc..

        if (_logPath != null)
        {
            csb.Append(_logPath.FileName);
            csb.Append(" | ");
        }

        for (var i = 0; i < _filterViewStates.Length; ++i)
        {
            if (i != 0)
                csb.Append(" -> ");

            if (i == _currentFilterIndex)
                csb.Append('[');

            csb.Append(i+1);
            csb.Append(':');
            csb.Append(_filterViewStates[i].current+1);
            csb.Append('/');
            csb.Append(_filterViewStates[i].total);

            if (i == _currentFilterIndex)
                csb.Append(']');
        }

        Screen.OutSetCursorPos(0, Top);
        Screen.OutSetForegroundColor(128, 128, 128);
        Screen.OutPrint(csb.Span, Width, true);
        Screen.OutResetAttributes();

        _changed = false;
    }
}
