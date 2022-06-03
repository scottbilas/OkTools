using System.Diagnostics;
using OkTools.Core;

class FilterChainView : ViewBase
{
    readonly List<ScrollingTextView> _views = new();
    int _currentIndex = -1;

    public FilterChainView(Screen screen) : base(screen) {}

    public IReadOnlyList<ScrollingTextView> Filters => _views;
    public int CurrentIndex => _currentIndex;
    public ScrollingTextView Current => _views[_currentIndex];

    public override void SetBounds(int width, int top, int bottom)
    {
        base.SetBounds(width, top, bottom);
        Current.SetBounds(width, top, bottom);
    }

    public void Draw() => Current.Draw();
    public bool HandleEvent(ITerminalEvent evt) => Current.HandleEvent(evt);

    public void SetCurrentIndex(int index)
    {
        if (index == _currentIndex)
            return;

        if (_currentIndex >= 0)
            Current.Enabled = false;

        _currentIndex = index;
        Current.Enabled = true;

        if (Height != 0)
            Current.SetBounds(Width, Top, Bottom, true);
    }

    public void AddAndActivate(ILogSource logSource)
    {
        Debug.Assert(_currentIndex == _views.Count - 1, "Insertion not supported yet");

        _views.Add(new ScrollingTextView(Screen, logSource) { Enabled = true });
        SetCurrentIndex(_views.Count - 1);
    }

    public void AddAndActivateChild()
    {
        Debug.Assert(_currentIndex == _views.Count - 1, "Insertion not supported yet");

        AddAndActivate(new FilterLogSource(Current.LogSource));
    }

    public void ActivateNext()
    {
        if (_currentIndex < _views.Count - 1)
            SetCurrentIndex(_currentIndex + 1);
    }

    public void ActivatePrev()
    {
        if (_currentIndex > 0)
            SetCurrentIndex(_currentIndex - 1);
    }

    public void RemoveLast()
    {
        _views.DropBack();

        _views[^1].Enabled = true;
        _currentIndex = _views.Count - 1;
        Current.SetBounds(Width, Top, Bottom, true);
    }
}
