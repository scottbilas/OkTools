using System.Diagnostics;

namespace OkTools.Core;

[PublicAPI]
public static partial class StaticUtility
{
    [DebuggerStepThrough]
    public static T[] Arr<T>(params T[] items) => items;

    public static int MinimizeWith(this ref int @this, int other) => @this = Math.Min(@this, other);
    public static int MaximizeWith(this ref int @this, int other) => @this = Math.Max(@this, other);
}
