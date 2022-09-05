namespace OkTools.Core;

[PublicAPI]
public class ArrayUtility
{
    public static int BinarySearch<TElement, TSelected>(IReadOnlyList<TElement> array, TSelected value, Func<TElement, TSelected> selector)
        where TSelected : IComparable<TSelected>
    {
        for (var (lo, hi) = (0, array.Count - 1); lo <= hi;)
        {
            var mid = lo + (hi - lo) / 2;
            switch (selector(array[mid]).CompareTo(value))
            {
                case < 0: lo = mid + 1; break;
                case > 0: hi = mid - 1; break;
                default:
                    return mid;
            }
        }
        return -1;
    }
}
