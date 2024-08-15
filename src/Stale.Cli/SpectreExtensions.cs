using Spectre.Console;
using Spectre.Console.Advanced;
using Spectre.Console.Rendering;
using Vezel.Cathode.Text.Control;

[PublicAPI]
static class SpectreExtensions
{
    // ControlBuilder

    public static void Print(this ControlBuilder @this, string text, Style style) =>
        @this.Print(new Text(text, style).ToAnsi());
    public static void PrintLine(this ControlBuilder @this, string text, Style style) =>
        @this.PrintLine(new Text(text, style).ToAnsi());

    public static void PrintMarkup(this ControlBuilder @this, string markup, Style? style = null) =>
        @this.Print(new Markup(markup, style).ToAnsi());
    public static void PrintMarkupInterp(this ControlBuilder @this, FormattableString markup, Style? style = null) =>
        @this.Print(Markup.FromInterpolated(markup, style).ToAnsi());

    public static void PrintMarkupLine(this ControlBuilder @this, string markup, Style? style = null) =>
        @this.PrintLine(new Markup(markup, style).ToAnsi());
    public static void PrintMarkupInterpLine(this ControlBuilder @this, FormattableString markup, Style? style = null) =>
        @this.PrintLine(Markup.FromInterpolated(markup, style).ToAnsi());

    // IRenderable

    public static string ToAnsi(this IRenderable @this) =>
        AnsiConsole.Console.ToAnsi(@this);
}
