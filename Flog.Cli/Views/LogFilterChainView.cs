using System.Diagnostics;
using OkTools.Core;

class LogFilterChainView : ViewBase
{
    readonly List<ScrollingTextView> _views = new();
    int _currentIndex = -1;

    public LogFilterChainView(Screen screen) : base(screen) {}

    public IReadOnlyList<ScrollingTextView> Filters => _views;
    public int CurrentIndex => _currentIndex;
    public ScrollingTextView Current => _views[_currentIndex];

    public override void SetBounds(int width, int top, int bottom)
    {
        base.SetBounds(width, top, bottom);
        Current.SetBounds(width, top, bottom);
    }

    public void Draw() => Current.Draw();
    public void UpdateAndDrawIfChanged() => Current.Update(true);
    public bool HandleEvent(ITerminalEvent evt) => Current.HandleEvent(evt);

    public void SetCurrentIndex(int index)
    {
        if (index == _currentIndex)
            return;

        if (_currentIndex >= 0)
            Current.Enabled = false;

        _currentIndex = index;
        Current.Enabled = true;
        Current.Update(false);

        if (Height != 0)
            Current.SetBounds(Width, Top, Bottom, true);
    }

    public void SafeSetCurrentIndex(int index)
    {
        SetCurrentIndex(Math.Clamp(index, 0, _views.Count - 1));
    }

    public void AddAndActivate(LogProcessor logSource)
    {
        _views.Add(new ScrollingTextView(Screen, logSource) { Enabled = true });
        ActivateLast();
    }

    public void ActivateNext() => SafeSetCurrentIndex(_currentIndex + 1);
    public void ActivatePrev() => SafeSetCurrentIndex(_currentIndex - 1);
    public void ActivateLast() => SafeSetCurrentIndex(_views.Count - 1);

    public void RemoveLast()
    {
        Debug.Assert(_views.Count > 1); // don't delete all the views, kill the whole chain instead..

        _views.DropBack();
        _currentIndex = -1;
        SetCurrentIndex(_views.Count - 1);
    }
}
