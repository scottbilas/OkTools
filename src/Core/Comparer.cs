namespace OkTools.Core;

public static class DelegateComparer
{
    public static IComparer<T>? Create<T>(Func<T?, T?, int>? comparer) =>
        comparer != null ? new DelegateComparer<T>(comparer) : null;
}

class DelegateComparer<T>(Func<T?, T?, int> comparer) : IComparer<T>
{
    public int Compare(T? x, T? y) => comparer(x, y);
}
