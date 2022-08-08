using System.Diagnostics;

namespace OkTools.Core;

[PublicAPI]
public static partial class StaticUtility
{
    [DebuggerStepThrough]
    public static T[] Arr<T>(params T[] items) => items;
}
