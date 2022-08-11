using System.Diagnostics;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization.NamingConventions;

namespace OkTools.Unity;

public class UnityVersionFormatException : Exception
{
    static string MakeMessage(string versionText) =>
        $"Unexpected Unity version format: '{versionText}'";

    public UnityVersionFormatException(string versionText, Exception innerException)
        : base(MakeMessage(versionText), innerException) {}
    public UnityVersionFormatException(string versionText)
        : base(MakeMessage(versionText)) {}
}

// this is a 'fuzzy' version class. all fields except the major are optional.
[PublicAPI]
public class UnityVersion : IEquatable<UnityVersion>, IComparable<UnityVersion>, IComparable
{
    // a note on hash length.. it varies.
    //
    // * sometimes we're given a complete hash (long)
    // * typically people work with nicer 12-char hashes, so we use that for our string rep
    // * old hg-based versions of unity have shorter (6-char, also in decimal) hashes
    // * newer versions of unity use 9-char hashes (not sure if this is by design, asked in slack to see)

    const int k_niceHashLength = 12;

    // fields from Runtime/Utilities/UnityVersion.h

    public readonly int     Major;
    public readonly int?    Minor, Revision;
    public readonly char?   ReleaseType; // alpha, beta, public, patch, experimental
    public readonly int?    Incremental;
    public readonly string? Branch;
    public readonly string? Hash;

    public UnityVersion(
        int     major,
        int?    minor       = null,
        int?    revision    = null,
        char?   releaseType = null,
        int?    incremental = null,
        string? branch      = null,
        string? hash        = null)
    {
        Major       = major;
        Minor       = minor;
        Revision    = revision;
        ReleaseType = releaseType;
        Incremental = incremental;

        if (branch == "")
            branch = null;
        Branch = branch;

        if (hash == "")
            hash = null;

        if (hash != null && Regex.IsMatch(hash, @"[^a-fA-F0-9]"))
            throw new ArgumentException($"Hash '{hash}' contains invalid characters", nameof(hash));

        Hash = hash;
    }

    public override string ToString()
    {
        var str = Major.ToString();
        if (Minor.HasValue)
        {
            str += $".{Minor.Value}";
            if (Revision.HasValue)
            {
                str += $".{Revision.Value}";
                if (ReleaseType.HasValue)
                {
                    str += ReleaseType.Value;
                    if (Incremental.HasValue)
                    {
                        str += Incremental.Value;
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(Branch))
            str += $"-{Branch}";
        if (!string.IsNullOrEmpty(Hash))
            str += $"_{Hash.Left(k_niceHashLength)}";

        return str;
    }

    public UnityVersion StripHash() => new(Major, Minor, Revision, ReleaseType, Incremental, Branch);

    static bool IsHashSame(string? a, string? b)
    {
        if (a is null && b is null)
            return true;
        if (a is null || b is null)
            return false;

        return a.Length > b.Length
            ? a.StartsWith(b, StringComparison.OrdinalIgnoreCase)
            : b.StartsWith(a, StringComparison.OrdinalIgnoreCase);
    }

    public bool Equals(UnityVersion? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Major == other.Major
            && Minor == other.Minor
            && Revision == other.Revision
            && ReleaseType == other.ReleaseType
            && Incremental == other.Incremental
            && string.Equals(Branch, other.Branch, StringComparison.OrdinalIgnoreCase)
            && IsHashSame(Hash, other.Hash);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;

        return obj.GetType() == GetType() && Equals((UnityVersion)obj);
    }

    // Because Equals() uses IsHashSame we risk separate UnityVersions getting different hashcodes but comparing
    // equal. The caller needs to figure out how to handle this, can't have an automatic solution here.
    public override int GetHashCode() =>
        throw new NotSupportedException("Must hash explicitly");

    public int CompareTo(UnityVersion? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (other is null) return 1;

        var majorComparison = Major.CompareTo(other.Major);
        if (majorComparison != 0) return majorComparison;
        var minorComparison = Nullable.Compare(Minor, other.Minor);
        if (minorComparison != 0) return minorComparison;
        var revisionComparison = Nullable.Compare(Revision, other.Revision);
        if (revisionComparison != 0) return revisionComparison;
        var releaseTypeComparison = Nullable.Compare(ReleaseType, other.ReleaseType);
        if (releaseTypeComparison != 0) return releaseTypeComparison;
        var incrementalComparison = Nullable.Compare(Incremental, other.Incremental);
        if (incrementalComparison != 0) return incrementalComparison;
        var branchComparison = string.Compare(Branch, other.Branch, StringComparison.OrdinalIgnoreCase);
        if (branchComparison != 0) return branchComparison;

        if (IsHashSame(Hash, other.Hash)) return 0;
        return string.Compare(Hash, other.Hash, StringComparison.OrdinalIgnoreCase);
    }

    public int CompareTo(object? obj)
    {
        if (ReferenceEquals(null, obj)) return 1;
        if (ReferenceEquals(this, obj)) return 0;

        return obj is UnityVersion other
            ? CompareTo(other)
            : throw new ArgumentException($"Object must be of type {nameof(UnityVersion)}");
    }

    public bool IsMatch(UnityVersion other)
    {
        if (Major != other.Major) return false;

        if (Minor.HasValue && other.Minor.HasValue && Minor.Value != other.Minor.Value) return false;
        if (Revision.HasValue && other.Revision.HasValue && Revision.Value != other.Revision.Value) return false;
        if (ReleaseType.HasValue && other.ReleaseType.HasValue && ReleaseType.Value != other.ReleaseType.Value) return false;
        if (Incremental.HasValue && other.Incremental.HasValue && Incremental.Value != other.Incremental.Value) return false;
        if (Branch != null && other.Branch != null && !string.Equals(Branch, other.Branch, StringComparison.OrdinalIgnoreCase)) return false;
        if (Hash != null && other.Hash != null && !IsHashSame(Hash, other.Hash)) return false;

        return true;
    }

    // Operators

    public static bool operator ==(UnityVersion? left, UnityVersion? right) => Equals(left, right);
    public static bool operator !=(UnityVersion? left, UnityVersion? right) => !Equals(left, right);
    public static bool operator < (UnityVersion? left, UnityVersion? right) => Comparer<UnityVersion>.Default.Compare(left, right) < 0;
    public static bool operator > (UnityVersion? left, UnityVersion? right) => Comparer<UnityVersion>.Default.Compare(left, right) > 0;
    public static bool operator <=(UnityVersion? left, UnityVersion? right) => Comparer<UnityVersion>.Default.Compare(left, right) <= 0;
    public static bool operator >=(UnityVersion? left, UnityVersion? right) => Comparer<UnityVersion>.Default.Compare(left, right) >= 0;

    // Static ctors

    public enum NormalizeLegacy { No, Yes }

    public static UnityVersion FromText(string versionText, NormalizeLegacy normalizeLegacy = NormalizeLegacy.No) =>
        TryFromText(versionText, normalizeLegacy) ?? throw new UnityVersionFormatException(versionText);

    // TODO: add a TryFromText variant that can also take a list of versions (or toolchains) and do a "match one of these".
    //       (very important for, at least, version-from-hash)

    public static UnityVersion? TryFromText(string versionText, NormalizeLegacy normalizeLegacy = NormalizeLegacy.No)
    {
        var m = Regex.Match(versionText, @"^(?imnx-s)
            (unityhub://)?
            (?<Major>\d+)
            (\.(?<Minor>\d+)
             (\.(?<Revision>\d+)
              ((?<ReleaseType>[a-z])
               (?<Incremental>\d+)?
            )?)?)?
            (-(?<Branch>[^_/]+))?
            ([_/](?<Hash>[a-f0-9]{6,}))?$");

        if (m.Success)
        {
            string? Get(string name)
            {
                var g = m.Groups[name];
                return g.Success ? g.Value : null;
            }

            T? Parse<T>(string name, Func<string, T> convert) where T : struct
            {
                var str = Get(name);
                return str != null ? convert(str) : null;
            }

            return new UnityVersion(
                int.Parse(m.Groups["Major"].Value),
                Parse("Minor", int.Parse),
                Parse("Revision", int.Parse),
                Parse("ReleaseType", s => s[0]),
                Parse("Incremental", int.Parse),
                Get("Branch"),
                Get("Hash"));
        }

        if (normalizeLegacy != NormalizeLegacy.Yes)
            return null;

        m = Regex.Match(versionText, @"^(?<Major>\d+)\.(?<Minor>\d+)\.(?<Revision>\d+)\.(?<Hash>\d+)$");
        if (!m.Success)
            return null;

        // it's an older code, sir, but it checks out
        return new UnityVersion(
            int.Parse(m.Groups["Major"].Value),
            int.Parse(m.Groups["Minor"].Value),
            int.Parse(m.Groups["Revision"].Value),
            hash: int.Parse(m.Groups["Hash"].Value).ToString("x6")); // old style is hg hash as decimal
    }

    public static UnityVersion FromFileVersionInfo(FileVersionInfo fileVersionInfo)
    {
        if (fileVersionInfo.ProductVersion == null)
            throw new ArgumentException("Expected ProductVersion is missing", nameof(fileVersionInfo));

        // because we're using ProductVersion, we may run across old style version number encoding. enable that.
        return FromText(fileVersionInfo.ProductVersion, NormalizeLegacy.Yes);
    }

    public static UnityVersion FromUnityExe(string pathToUnityExe) =>
        FromFileVersionInfo(FileVersionInfo.GetVersionInfo(pathToUnityExe));

    public static UnityVersion FromUnityBuildFolder(string pathToUnityBuild) =>
        FromUnityExe(pathToUnityBuild.ToNPath().Combine(UnityConstants.UnityExeName));

    public static UnityVersion FromUnityProjectVersionTxt(string pathToVersionTxt)
    {
        var projectVersionDb = File
            .ReadLines(pathToVersionTxt)
            .Select(l => l.Split(':', 2))
            .Where(l => l.Length > 1)
            .ToDictionary(l => l[0].Trim(), l => l[1].Trim());

        if (!projectVersionDb.TryGetValue("m_EditorVersionWithRevision", out var versionTxt))
            return FromText(projectVersionDb["m_EditorVersion"]);

        var match = Regex.Match(versionTxt, @"(?<ver>\S+)\s*\((?<hash>[^)]+)\)");
        return FromText($"{match.Groups["ver"]}_{match.Groups["hash"]}");
    }

    public static IEnumerable<UnityVersion> FromEditorsYml(string pathToEditorsYml)
    {
        using var reader = new StreamReader(pathToEditorsYml);

        var editorsYml = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build()
            .Deserialize<EditorsYml>(reader);

        // TODO: figure out how to ignore extra stuff in .yml (IgnoreUnmatchedProperties) but WITHOUT losing the ability
        // to have the yml parser ensure every field in the deserialized class is filled out.
        if (editorsYml.EditorVersions == null ||
            editorsYml.EditorVersions.Values.Any(v => v.Version == null || v.Revision == null))
        {
            throw new UnityVersionFormatException($"Failure parsing yml in '{pathToEditorsYml}'");
        }

        foreach (var editorVersion in editorsYml.EditorVersions.Values)
            yield return FromText($"{editorVersion.Version}_{editorVersion.Revision}");
    }

    #pragma warning disable CS0649
    class EditorsYml
    {
        public Dictionary<string, EditorVersion>? EditorVersions;

        // ReSharper disable once ClassNeverInstantiated.Local
        public class EditorVersion
        {
            public string? Version, Revision;
        }
    }
    #pragma warning restore CS0649
}
