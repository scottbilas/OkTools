using DocoptNet;
using NiceIO;

static class Extensions
{
    public static IEnumerable<string> AsStrings(this ValueObject @this) =>
        @this.AsList.Cast<string>();

    public static string ToNiceString(this NPath @this) =>
        @this.TildeCollapse().ToString(SlashMode.Forward);
}
