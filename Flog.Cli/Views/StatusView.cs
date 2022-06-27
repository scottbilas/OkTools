using OkTools.Flog;

class StatusView : ViewBase
{
    record struct FilterStatus(int Count, int CountWhenLastActive, int ScrollPos, bool IsFollowing, WrapType WrapType)
    {
        public bool HasNewData => Count != CountWhenLastActive;
    }

    // TODO: rethink this whole thing..maybe just go with a simple double buffering of the char[]
    readonly char[] _buffer = new char[100];
    char[] _text = Array.Empty<char>();
    bool _changed = true;

    NPath? _logPath;
    int _currentFilterIndex;
    FilterStatus[] _filterStatuses = Array.Empty<FilterStatus>();

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

    public void Update(LogModel logModel, LogView logView)
    {
        CheckEnabled();

        if (_currentFilterIndex != logView.CurrentIndex)
        {
            _currentFilterIndex = logView.CurrentIndex;
            _changed = true;
        }

        if (_filterStatuses.Length != logView.FilterViews.Count)
        {
            // TODO: this will be a problem when i allow insert/remove on filters (not just at the end)
            Array.Resize(ref _filterStatuses, logView.FilterViews.Count);
            _changed = true;
        }

        for (var i = 0; i < _filterStatuses.Length; ++i)
        {
            var state = new FilterStatus
            {
                Count = logModel.GetItemCount(i),
                ScrollPos = logView.FilterViews[i].ScrollPos,
                IsFollowing = logView.FilterViews[i].IsFollowing,
                WrapType = logView.FilterViews[i].WrapType,
            };

            state.CountWhenLastActive = i == _currentFilterIndex
                ? state.Count // count is current while we look at it
                : _filterStatuses[i].CountWhenLastActive; // maintain previous count

            if (_filterStatuses[i] != state)
            {
                _filterStatuses[i] = state;
                _changed = true;
            }
        }

        if (_changed)
            Draw();
    }

    public void Draw()
    {
        CheckEnabled();

        // TODO: CSB needs to have some ControlBuilder abilities.. want
        //     * ability to prevent writing past end of screen, but also clear to end (currently handled by `OutPrint(span, width, true)`)
        //     * support for some basic attribute-only (color, underline etc.) control sequences, which will then not count against Width

        var text = new CharSpanBuilder(_text);

        // TODO: fix this mess..not a great way to render sectional status bar..

        if (_logPath != null)
        {
            // TODO: assign 1/3 or something, trunc with "..."
            text.AppendTrunc(_logPath.FileName);
            text.TryAppend(" | ");
        }

        var right = new CharSpanBuilder(_buffer);
        right.Append(" | ");
        right.Append(_filterStatuses[_currentFilterIndex].IsFollowing ? 'f' : ' ');
        right.Append(_filterStatuses[_currentFilterIndex].WrapType switch
            { WrapType.Rigid => 'w', WrapType.Word => 'W', _ => ' ' });

        for (var i = 0; i < _filterStatuses.Length && text.UnusedLength > right.Length; ++i)
        {
            var mid = new CharSpanBuilder(_buffer);

            if (i != 0)
                mid.Append(" -> ");

            if (i == _currentFilterIndex)
                mid.Append('[');

            mid.Append(i + 1);
            mid.Append(':');
            mid.Append(_filterStatuses[i].ScrollPos + 1);
            mid.Append('/');
            mid.Append(_filterStatuses[i].Count);
            if (_filterStatuses[i].HasNewData)
                mid.Append('*');

            if (i == _currentFilterIndex)
                mid.Append(']');

            text.AppendTrunc(mid);
        }

        text.Length = Math.Clamp(Width - right.Length, 0, text.Length);
        text.Append(' ', Math.Max(0, Width - text.Length - right.Length));
        text.AppendTrunc(right);

        Screen.OutSetCursorPos(0, Top);
        Screen.OutSetForegroundColor(128, 128, 128);
        Screen.OutPrint(text, Width, true);
        Screen.OutResetAttributes();

        _changed = false;
    }
}
