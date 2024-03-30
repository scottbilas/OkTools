namespace OkTools.Core;

[PublicAPI]
public static class TimeSpanExtensions
{
    public static TimeSpan Sum(this IEnumerable<TimeSpan> @this) =>
        @this.Aggregate(default(TimeSpan), (acc, next) => acc + next);

#   if NETSTANDARD
    public static double TotalMicroseconds(this TimeSpan @this) => (double)@this.Ticks / 10;
    public static int Microseconds(this TimeSpan @this) => (int)(@this.Ticks / 10 % 1000);
#   else
    public static double TotalMicroseconds(this TimeSpan @this) => @this.TotalMicroseconds;
    public static int Microseconds(this TimeSpan @this) => @this.Microseconds;
#   endif

    public static string ToNiceString(this TimeSpan? @this, bool limitGranularityToSeconds = false) =>
        @this != null ? ToNiceString(@this.Value) : "(null)";

    // TODO: merge this with ToNiceAge
    public static string ToNiceString(this TimeSpan @this, bool limitGranularityToSeconds = false)
    {
        if (!limitGranularityToSeconds)
        {
            if (@this.TotalMilliseconds < 5 && @this.Microseconds() != 0) // don't bother printing usec if it's zero (most likely this TimeSpan was constructed direct from msec)
                return $"{(int)@this.TotalMicroseconds()}us";
            if (@this.TotalSeconds < 5)
                return $"{@this.TotalMilliseconds:0}ms";
        }
        if (@this.TotalMinutes < 1)
            return $"{@this.TotalSeconds:0.0}s";
        if (@this.TotalHours < 1)
            return $"{(int)@this.TotalMinutes}m {@this.Seconds}s";
        if (@this.TotalDays < 1)
            return $"{(int)@this.TotalHours}h {@this.Minutes}m {@this.Seconds}s";
        return $"{(int)@this.TotalDays}d {@this.Hours}h {@this.Minutes}m";
    }

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
