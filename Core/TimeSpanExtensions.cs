namespace OkTools.Core;

[PublicAPI]
public static class TimeSpanExtensions
{
    public static string ToNiceAge(this TimeSpan @this)
    {
        // TODO !! TESTS

        var days = @this.TotalDays;
        switch (days)
        {
            case > 365*2:
                return (days/365f).ToString("0.0yr");
            case > 30*2:
                return (days/30f).ToString("0mo");
            case > 7*2:
                return (days/7f).ToString("0wk");
        }

        if (@this.Days > 0)
            return $"{@this.Days}d{@this.Hours}h";

        if (@this.Hours > 0)
            return $"{@this.Hours}h{@this.Minutes}m";

        if (@this.Minutes > 0)
            return $"{@this.Minutes}m{@this.Seconds}s";

        if (@this.Seconds > 0)
            return $"{@this.TotalSeconds:0.0}s";

        return "now";
    }
}
