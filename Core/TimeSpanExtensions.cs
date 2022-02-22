namespace OkTools.Core;

[PublicAPI]
public static class TimeSpanExtensions
{
    public static string ToNiceAge(this TimeSpan @this, bool ago = false)
    {
        var agoText = ago ? " ago" : "";

        var days = @this.TotalDays;
        switch (days)
        {
            case > 365*2:
                return (days/365f).ToString("0.0yr") + agoText;
            case > 30*2:
                return (days/30f).ToString("0.0mo") + agoText;
            case > 7*2:
                return (days/7f).ToString("0.0wk") + agoText;
        }

        if (@this.Days > 0)
            return $"{@this.Days}d{@this.Hours}h{agoText}";

        if (@this.Hours > 0)
            return $"{@this.Hours}h{@this.Minutes}m{agoText}";

        if (@this.Minutes > 0)
            return $"{@this.Minutes}m{@this.Seconds}s{agoText}";

        if (@this.Seconds > 10)
            return $"{@this.TotalSeconds:0.0}s{agoText}";

        if (@this.TotalSeconds > 0)
            return $"{@this.TotalSeconds:0.00}s{agoText}";

        return "now";
    }
}
