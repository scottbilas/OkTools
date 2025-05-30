namespace OkTools.Terminal;

using DocoptNet;

public readonly record struct NamedOpt<T>(string Name, T? Value);

public static class NamedValue
{
    public static NamedOpt<T> Create<T>(string name, T? value) => new(name, value);
}

// stay with IBaselineParser; i want to handle versioning and help myself (don't like how docopt.net does it)
public interface ICliDocoptArgs
{
    NamedOpt<string>? OptPauseLevel => null;
    NamedOpt<string>? OptDebugLevel => null;
    NamedOpt<string>? OptOutputFormat => null;

    bool OptVersion { get; }
    string? ArgCommand { get; }
    IReadOnlyList<string> ArgCommandArgs { get; }
}

public static partial class DocoptUtils
{
    // TODO: update docopt codegen to do this as generated attrs or something
    public static string OptNameToText(string optPropertyName)
    {
        if (!optPropertyName.StartsWith("Opt", StringComparison.Ordinal))
            throw new ArgumentException("Expecting option arguments only");

        if (optPropertyName.Length == 4)
            return "-" + char.ToLower(optPropertyName[3]);

        var len =
            optPropertyName.Length  // string
            - 3  // skip Opt
            + 2  // room for leading '--'
            + optPropertyName.Count(char.IsUpper) - 2; // every capital is preceded by -, except the first and the 'O' in Opt

        Span<char> chars = stackalloc char[len];
        chars[0] = '-';
        chars[1] = '-';
        chars[2] = char.ToLower(optPropertyName[3]);

        for (var (i, o) = (4, 3); i != optPropertyName.Length; ++i)
        {
            if (char.IsUpper(optPropertyName[i]))
                chars[o++] = '-';
            chars[o++] = char.ToLower(optPropertyName[i]);
        }

        return new string(chars);
    }

    public static bool ParseEnumOpt<T>(string optionName, string? optionValue, out T result, T defaultValue = default) where T : struct, Enum
    {
        if (optionValue == null)
        {
            result = defaultValue;
            return false;
        }

        if (Enum.TryParse(optionValue, true, out result))
            return true;

        throw new DocoptInputErrorException(
            $"Illegal `{OptNameToText(optionName)}` type '{optionValue}' "+
            $"(must be one of [{Enum.GetNames<T>().Select(s => s.ToLower()).StringJoin(' ')}])");
    }

    public static T ParseEnumOpt<T>(string optionName, string? optionValue, T defaultValue = default) where T : struct, Enum
    {
        ParseEnumOpt(optionName, optionValue, out var result, defaultValue);
        return result;
    }

    public static bool ParseEnumOpt<T>(NamedOpt<string>? option, out T result, T defaultValue = default) where T : struct, Enum
    {
        if (option == null)
        {
            result = defaultValue;
            return false;
        }
        return ParseEnumOpt(option.Value.Name, option.Value.Value, out result, defaultValue);
    }

    public static T ParseEnumOpt<T>(NamedOpt<string>? option, T defaultValue = default) where T : struct, Enum
    {
        ParseEnumOpt(option, out var result, defaultValue);
        return result;
    }

    // pass in your generated args class instance and it will give back a line of switches and then one line per flag with arg(s).
    // useful for printing a nice command line for debug purposes.
    public static IEnumerable<string> DumpArguments(IEnumerable<KeyValuePair<string, object?>> args)
    {
        var options = args
            .Select(kv => kv.Value switch
            {
                true => kv.Key,
                string str => $"{kv.Key} {str}",
                StringList { Count: > 0 } list => $"{kv.Key} {list.StringJoin(' ')}",
                null or false or StringList => null,
                _ => throw new NotSupportedException($"Unknown option value type {kv.Value.GetType().Name}")
            })
            .WhereNotNull()
            .ToArray();

        var switches = options.Where(v => !v.Contains(' ')).OrderBy(v => v.StartsWith('-')).ThenBy(v => v).StringJoin(' ');
        if (switches.Any())
            yield return switches;

        foreach (var option in options.Where(v => v.Contains(' ')).Ordered())
            yield return option;
    }
}
