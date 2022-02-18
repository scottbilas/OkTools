namespace OkTools.Core;

// TODO: consider ditching this and going with https://github.com/TylerBrinkley/Enums.NET or similar

[PublicAPI]
public static class EnumUtility
{
    public static int GetCount<T>()
        where T: struct, Enum =>
        GetNames<T>().Count;

    public static IReadOnlyList<string> GetNames<T>()
        where T: struct, Enum =>
        s_nameCache.GetValueOr(typeof(T)) ?? (s_nameCache[typeof(T)] = Enum.GetNames(typeof(T)));

    public static IReadOnlyList<string> GetLowercaseNames<T>()
        where T: struct, Enum =>
        GetOrAddLowerNameCache(typeof(T));

    public static IReadOnlyList<T> GetValues<T>()
        where T: struct, Enum
    {
        var found =
            s_valueCache.GetValueOr(typeof(T))
            ?? (s_valueCache[typeof(T)] = Enum.GetValues(typeof(T)));
        return (IReadOnlyList<T>)found;
    }

    public static T TryParseIgnoreCase<T>(string enumName, T defaultValue = default)
        where T: struct, Enum =>
        Enum.TryParse(enumName, true, out T value) ? value : defaultValue;

    public static T TryParse<T>(string enumName, T defaultValue = default)
        where T: struct, Enum =>
        Enum.TryParse(enumName, false, out T value) ? value : defaultValue;

    public static T? TryParseIgnoreCaseOr<T>(string enumName)
        where T: struct, Enum =>
        Enum.TryParse(enumName, true, out T value) ? value : null;

    public static T? TryParseOr<T>(string enumName)
        where T: struct, Enum =>
        Enum.TryParse(enumName, false, out T value) ? value : null;

    static IReadOnlyList<string> GetNameCache(Type enumType) =>
        s_nameCache.GetOrAdd(enumType, Enum.GetNames);

    static IReadOnlyList<string> GetOrAddLowerNameCache(Type enumType)
    {
        return s_lowercaseNameCache.GetOrAdd(enumType, t =>
        {
            var names = GetNameCache(t);
            var lowerNames = names.SelectToLower().Distinct().ToArray();

            // EnumUtility doesn't care about this, but it is probably a sign of a bug elsewhere
            if (names.Count != lowerNames.Length)
                throw new Exception($"Unexpected case insensitive duplicates found in enum {enumType.FullName}");

            return lowerNames;
        });
    }

    static Dictionary<Type, IReadOnlyList<string>> s_nameCache = new();
    static Dictionary<Type, IReadOnlyList<string>> s_lowercaseNameCache = new();
    static Dictionary<Type, object> s_valueCache = new();
}
