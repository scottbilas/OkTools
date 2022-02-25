using DocoptNet;
using DotNetConfig;
using NiceIO;

static class Extensions
{
    public static IEnumerable<string> AsStrings(this ValueObject @this) =>
        @this.AsList.Cast<string>();

    public static string ToNiceString(this NPath @this) =>
        @this.TildeCollapse().ToString(SlashMode.Forward);

    public static IEnumerable<string> GetAllStrings(this Config @this, string section, string? subsection, string variable)
        => @this.GetAll(section, subsection, variable, null).Select(v => v.GetString());
    public static IEnumerable<string> GetAllStrings(this Config @this, string section, string variable)
        => @this.GetAllStrings(section, null, variable);

    public static NPath? GetNPath(this Config @this, string section, string? subsection, string variable)
        => @this.TryGetString(section, subsection, variable, out var value) ? value.ToNPath() : null;
    public static NPath? GetNPath(this Config @this, string section, string variable)
        => @this.GetNPath(section, null, variable);
}
