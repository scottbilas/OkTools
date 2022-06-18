using System.Diagnostics;
using OkTools.Flog;

class LogView : ViewBase
{
    readonly LogModel _model;
    readonly List<ScrollingTextView> _views = new();
    int _currentIndex = -1;

    public LogView(Screen screen, LogModel model) : base(screen)
    {
        _model = model;
        AddAndActivate(new PassThruProcessor());
        Current.IsFollowing = true;
    }

    public int CurrentIndex => _currentIndex;
    public ScrollingTextView Current => _views[_currentIndex];
    public IReadOnlyList<ScrollingTextView> FilterViews => _views;

    public void Draw() => Current.Draw();
    public void Update(bool drawIfChanged) => Current.Update(drawIfChanged);
    public bool HandleEvent(ITerminalEvent evt) => Current.HandleEvent(evt);

    public override void SetBounds(int width, int top, int bottom)
    {
        base.SetBounds(width, top, bottom);
        Current.SetBounds(width, top, bottom);
    }

    public void SetCurrentIndex(int index)
    {
        if (index == _currentIndex)
            return;

        if (_currentIndex >= 0)
            Current.Enabled = false;

        _currentIndex = index;

        var current = Current;
        current.Enabled = true;
        current.Update(false);

        if (Enabled)
            current.SetBounds(Width, Top, Bottom, true);
    }

    public void SafeSetCurrentIndex(int index)
    {
        SetCurrentIndex(Math.Clamp(index, 0, _views.Count - 1));
    }

    public string CurrentFilterText
    {
        get => Current.Processor.To<SimpleFilterProcessor>().Filter;
        set => Current.Processor.To<SimpleFilterProcessor>().Filter = value;
    }

    public void AddAndActivateSimpleFilter()
    {
        AddAndActivate(new SimpleFilterProcessor());
    }

    void AddAndActivate(LogProcessorBase processor)
    {
        _model.Add(processor);
        _views.Add(new ScrollingTextView(Screen, processor) { Enabled = true });
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
