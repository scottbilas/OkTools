using System.Diagnostics;

namespace OkTools.Core;

[PublicAPI]
public static class StaticUtility
{
    [DebuggerStepThrough]
    public static T[] Arr<T>(params T[] items) => items;
}
