namespace OkTools.Core;

public static class CharExtensions
{
    public static char ToUpper(this char @this) => char.ToUpperInvariant(@this);
    public static char ToLower(this char @this) => char.ToLowerInvariant(@this);
}
