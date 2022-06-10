class StatusView : ViewBase
{
    record struct FilterStatus(int Count, int CountWhenLastActive, int ScrollPos, bool IsFollowing)
    {
        public bool HasNewData => Count != CountWhenLastActive;
    }

    // TODO: rethink this whole thing..maybe just go with a simple double buffering of the char[]
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
                IsFollowing = logView.FilterViews[i].IsFollowing
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

        var csb = new CharSpanBuilder(_text);

        // TODO: "TryAppend", put in sections with center/left/etc-aligning, truncate, deal with pathological widths, etc..

        if (_logPath != null)
        {
            csb.Append(_logPath.FileName);
            csb.Append(" | ");
        }

        for (var i = 0; i < _filterStatuses.Length; ++i)
        {
            if (i != 0)
                csb.Append(" -> ");

            if (i == _currentFilterIndex)
                csb.Append('[');

            csb.Append(i + 1);
            if (_filterStatuses[i].IsFollowing)
                csb.Append('f');
            csb.Append(':');
            csb.Append(_filterStatuses[i].ScrollPos + 1);
            csb.Append('/');
            csb.Append(_filterStatuses[i].Count);
            if (_filterStatuses[i].HasNewData)
                csb.Append('*');

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
