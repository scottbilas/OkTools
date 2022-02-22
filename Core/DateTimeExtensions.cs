namespace OkTools.Core;

[PublicAPI]
public static class DateTimeExtensions
{
    public static string ToNiceAge(this DateTime @this, bool ago = false) =>
        (DateTime.Now - @this).ToNiceAge(ago);
}
