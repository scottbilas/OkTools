using System.Diagnostics;

namespace OkTools.Core;

[PublicAPI]
public static partial class StaticUtility
{
    [DebuggerStepThrough]
    public static T[] Arr<T>(params T[] items) => items;

    public static void Minimize(ref int @this, in int other) => @this = Math.Min(@this, other);
    public static void Maximize(ref int @this, in int other) => @this = Math.Max(@this, other);
}
