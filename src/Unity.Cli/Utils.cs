using DocoptNet;
using DotNetConfig;
using NiceIO;
using OkTools.Unity;

static class Extensions
{
    // DocoptNet extensions

    public static IEnumerable<string> AsStrings(this ValueObject @this) =>
        @this.AsList.Cast<string>();

    // NPath extensions

    public static string ToNiceString(this NPath @this) =>
        @this.TildeCollapse().ToString(SlashMode.Forward);

    // DotNetConfig extensions

    public static IEnumerable<string> GetAllStrings(this Config @this, string section, string? subsection, string variable)
        => @this.GetAll(section, subsection, variable, null).Select(v => v.GetString());
    public static IEnumerable<string> GetAllStrings(this Config @this, string section, string variable)
        => @this.GetAllStrings(section, null, variable);

    public static NPath? GetNPath(this Config @this, string section, string? subsection, string variable)
        => @this.TryGetString(section, subsection, variable, out var value) ? value.ToNPath() : null;
    public static NPath? GetNPath(this Config @this, string section, string variable)
        => @this.GetNPath(section, null, variable);

    // UnityToolchain extensions

    // TODO: have this whole distinct-orderby chain in OkTools.Unity, with an outer function that decides using passed overlay-config
    public static IEnumerable<UnityToolchain> MakeNice(this IEnumerable<UnityToolchain> toolchains) => toolchains
        // there may be dupes in the list, so filter. and we want the defaults to come first, because they will have the correct origin.
        .DistinctBy(toolchain => toolchain.Path, StringComparer.OrdinalIgnoreCase)
        // nice to have newest stuff first, but don't include the hash because locally-built will probably have different hash for the same version
        .OrderByDescending(toolchain => toolchain.Version.StripHash())
        // put the things we are more likely to want to use first
        .ThenBy(toolchain => toolchain.Origin);
}
