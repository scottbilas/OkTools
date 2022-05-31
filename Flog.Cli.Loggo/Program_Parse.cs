using System.Text.RegularExpressions;

public static partial class Program
{
    static (float min, float max) ParseMinMaxFloat(string str)
    {
        var split = str.Split(',', 2);
        var min = float.Parse(split[0]);
        var max = split.Length == 2 ? float.Parse(split[1]) : min;
        return (min, max);
    }

    static (int min, int max) ParseMinMaxInt(string str)
    {
        var split = str.Split(',', 2);
        var min = int.Parse(split[0]);
        var max = split.Length == 2 ? int.Parse(split[1]) : min;
        return (min, max);
    }

    static long ParseSize(string str)
    {
        var match = Regex.Match(str, @"(\d+)([kmg]b)?", RegexOptions.IgnoreCase);
        if (!match.Success)
            throw new ArgumentException($"`{str}` is not a valid size");

        var multiplier = 1;
        if (match.Groups[2].Success)
        {
            multiplier *= match.Groups[2].Value[0] switch
            {
                'k' => 1024,
                'm' => 1024 * 1024,
                'g' => 1024 * 1024 * 1024,
                _ => throw new Exception("Regex fail")
            };
        }

        return long.Parse(match.Groups[1].Value) * multiplier;
    }
}
