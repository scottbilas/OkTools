using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OkTools.Unity;

public class UnityVersionFormatException : Exception
{
    public UnityVersionFormatException(string versionText)
        : base($"Unexpected Unity version format: '{versionText}'") {}
}

// this is a 'fuzzy' version class. all fields except the major are optional.
[PublicAPI]
public class UnityVersion : IEquatable<UnityVersion>, IComparable<UnityVersion>, IComparable
{
    // fields from Runtime/Utilities/UnityVersion.h

    public readonly int     Major;
    public readonly int?    Minor, Revision;
    public readonly char?   ReleaseType; // alpha, beta, public, patch, experimental
    public readonly int?    Incremental;
    public readonly string? Branch;
    public readonly string? Hash;

    public enum NormalizeLegacy { No, Yes }

    public UnityVersion(string version, NormalizeLegacy normalizeLegacy = NormalizeLegacy.No)
    {
        var m = Regex.Match(version, @"^(?imnx-s)
            (?<Major>\d+)
            (\.(?<Minor>\d+)
             (\.(?<Revision>\d+)
              ((?<ReleaseType>[a-z])
               (?<Incremental>\d+)?
            )?)?)?
            (-(?<Branch>[^_]+))?
            (_(?<Hash>[a-f0-9]{12,}))?$");
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

            Major       = int.Parse(m.Groups["Major"].Value);
            Minor       = Parse("Minor", int.Parse);
            Revision    = Parse("Revision", int.Parse);
            ReleaseType = Parse("ReleaseType", s => s[0]);
            Incremental = Parse("Incremental", int.Parse);
            Branch      = Get("Branch");
            Hash        = Get("Hash");

            if (Hash is { Length: > 12 })
                Hash = Hash[..12];
        }
        else if (normalizeLegacy == NormalizeLegacy.Yes)
        {
            m = Regex.Match(version, @"^(?<Major>\d+)\.(?<Minor>\d+)\.(?<Revision>\d+)\.(?<Hash>\d+)$");
            if (m.Success)
            {
                // it's an older code, sir, but it checks out

                Major    = int.Parse(m.Groups["Major"].Value);
                Minor    = int.Parse(m.Groups["Minor"].Value);
                Revision = int.Parse(m.Groups["Revision"].Value);
                Hash     = int.Parse(m.Groups["Hash"].Value).ToString("x6"); // old style is hg hash as decimal
            }
        }

        if (!m.Success)
            throw new UnityVersionFormatException(version);
    }

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

        if (hash != null)
        {
            if (Regex.IsMatch(hash, @"[^a-fA-F0-9]"))
                throw new ArgumentException($"Hash '{hash}' contains invalid characters", nameof(hash));
            if (hash.Length > 12)
                hash = hash[..12];
        }

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
            str += $"_{Hash}";

        return str;
    }

    public bool Equals(UnityVersion? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        return Major == other.Major
            && Minor == other.Minor
            && Revision == other.Revision
            && ReleaseType == other.ReleaseType
            && Incremental == other.Incremental
            && string.Equals(Branch, other.Branch, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Hash, other.Hash, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;

        return obj.GetType() == GetType()
        && Equals((UnityVersion)obj);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Major);
        hashCode.Add(Minor);
        hashCode.Add(Revision);
        hashCode.Add(ReleaseType);
        hashCode.Add(Incremental);
        hashCode.Add(Branch, StringComparer.OrdinalIgnoreCase);
        hashCode.Add(Hash, StringComparer.OrdinalIgnoreCase);
        return hashCode.ToHashCode();
    }

    public int CompareTo(UnityVersion? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (ReferenceEquals(null, other)) return 1;

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
        if (Branch != null && other.Branch != null && string.Compare(Branch, other.Branch, StringComparison.OrdinalIgnoreCase) != 0) return false;
        if (Hash != null && other.Hash != null && string.Compare(Hash, other.Hash, StringComparison.OrdinalIgnoreCase) != 0) return false;

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

    public static UnityVersion FromFileVersionInfo(FileVersionInfo fileVersionInfo)
    {
        if (fileVersionInfo.ProductVersion == null)
            throw new ArgumentException("Expected ProductVersion is missing", nameof(fileVersionInfo));

        // because we're using ProductVersion, we may run across old style version number encoding. enable that.
        return new UnityVersion(fileVersionInfo.ProductVersion, NormalizeLegacy.Yes);
    }

    public static UnityVersion FromUnityExe(string pathToUnityExe)
    {
        return FromFileVersionInfo(FileVersionInfo.GetVersionInfo(pathToUnityExe));
    }

    public static UnityVersion FromUnityBuildFolder(string pathToUnityBuild)
    {
        return FromUnityExe(pathToUnityBuild.ToNPath().Combine("unity.exe"));
    }

    public static UnityVersion FromUnityProjectVersionTxt(string pathToVersionTxt)
    {
        var projectVersionDb = File
            .ReadLines(pathToVersionTxt)
            .Select(l => l.Split(':', 2))
            .Where(l => l.Length > 1)
            .ToDictionary(l => l[0].Trim(), l => l[1].Trim());

        if (!projectVersionDb.TryGetValue("m_EditorVersionWithRevision", out var versionTxt))
            return new UnityVersion(projectVersionDb["m_EditorVersion"]);

        var match = Regex.Match(versionTxt, @"(?<ver>\S+)\s*\((?<hash>[^)]+)\)");
        return new UnityVersion($"{match.Groups["ver"]}_{match.Groups["hash"]}");

    }
}
