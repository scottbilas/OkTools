using System.Text.RegularExpressions;

namespace OkTools.Core;

// TODO XMLDoc :D

partial class NPath
{
    public static bool operator <(NPath? left, NPath? right) =>
        left is null ? right is not null : left.CompareTo(right) < 0;
    public static bool operator <=(NPath? left, NPath? right) =>
        left is null || left.CompareTo(right) <= 0;
    public static bool operator >(NPath? left, NPath? right) =>
        !(left <= right);
    public static bool operator >=(NPath? left, NPath? right) =>
        !(left < right);

    public static implicit operator string(NPath path) =>
        path.ToString();

    // TODO: decide whether to keep this, given "Elements" is gone
    /// <summary>
    /// Split path at the given element index, returning two paths that, if combined, result in the original path.
    /// The subPath begins at the split index, and must be valid within the range of [0,Depth].
    /// </summary>
    public (NPath basePath, NPath subPath) SplitAtElement(int elementIndex)
    {
	    if (elementIndex < 0 || elementIndex >= Depth)
		    throw new ArgumentOutOfRangeException(nameof(elementIndex), $"Out of range 0 <= {elementIndex} < {Depth}");

	    // TODO: implement this without OldNPath

	    var old = new OldNPath(this);
	    var basePath = new OldNPath(old.Elements.Take(elementIndex).ToArray(), old.IsRelative, old.DriveLetter);
	    var subPath = new OldNPath(old.Elements.Skip(elementIndex).ToArray(), true, null);

	    return (basePath, subPath);
    }

    public NPath? ParentContaining(string needle, bool returnAppended) =>
	    ParentContaining(needle.ToNPath(), returnAppended);

    public NPath? ParentContaining(NPath needle, bool returnAppended)
    {
	    var found = ParentContaining(needle);
	    if (found != null && returnAppended)
		    found = found.Combine(needle);

	    return found;
    }

    // TODO: make this actually stream
    public IEnumerable<string> ReadLines()
    {
	    foreach (var line in ReadAllLines())
		    yield return line;
    }

#   if NETSTANDARD
    static readonly Regex s_shellExpandRx = new(@"\$\w+");
    static Regex ShellExpandRx() => s_shellExpandRx;
#   else
    [GeneratedRegex(@"\$\w+")]
    private static partial Regex ShellExpandRx();
#   endif

    public NPath ShellExpand()
    {
        var path = ShellExpandRx()
            .Replace(_path, m => Environment.GetEnvironmentVariable(m.Value[1..]) ?? m.Value);

        // some env vars are like 'ProgramFiles(x86)' so just support %name% style expansion too
        if (path.Contains('%'))
            path = Environment.ExpandEnvironmentVariables(path);

        return path
            .ToNPath()
            .TildeExpand();
    }

    public NPath TildeExpand()
    {
	    // implementing only the most basic part of https://www.gnu.org/software/bash/manual/html_node/Tilde-Expansion.html

	    if (!IsRelative)
		    return this;

	    if (_path == "~")
		    return HomeDirectory;

	    if (_path.StartsWith("~/", StringComparison.Ordinal))
		    return HomeDirectory.Combine(_path[2..]);

	    return this;
    }

    public NPath TildeCollapse()
    {
	    var thisAbs = MakeAbsolute();
	    var homeDir = HomeDirectory;

	    if (_path == homeDir._path)
		    return "~";

	    if (!thisAbs.IsChildOf(HomeDirectory))
		    return this;

	    var relative = thisAbs.RelativeTo(homeDir);
	    return new NPath("~").Combine(relative);
    }

    public NPath Move(string dest, bool overwrite) =>
        Move(new NPath(dest), overwrite);

    public NPath Move(NPath dest, bool overwrite)
    {
	    if (IsRoot)
		    throw new NotSupportedException(
			    "Move is not supported on a root level directory because it would be dangerous:" + ToString());

	    if (dest.DirectoryExists())
		    return Move(dest.Combine(FileName), overwrite);

	    if (FileExists())
	    {
		    dest.EnsureParentDirectoryExists();

            var srcNativePath = ToString(SlashMode.Native);
            var dstNativePath = dest.ToString(SlashMode.Native);

#           if NETSTANDARD

            if (overwrite)
            {
                // emulate overwrite for .net standard which doesn't have this param (.net core has had it since 3.1)
                if (File.Exists(dstNativePath))
                    File.Replace(srcNativePath, dstNativePath, null); // null == no backup
                else
                    File.Move(srcNativePath, dstNativePath);
            }
            else
                FileSystem.Active.File_Move(this, dest); // TODO UPDATE TO SUPPORT OVERWRITE

#           else
		    File.Move(srcNativePath, dstNativePath, overwrite);
#           endif

		    return dest;
	    }

	    if (DirectoryExists())
	    {
		    if (overwrite)
			    throw new NotImplementedException("Overwrite not currently supported on a directory-move");

		    FileSystem.Active.Directory_Move(this, dest);
		    return dest;
	    }

	    throw new ArgumentException("Move() called on a path that doesn't exist: " + ToString());
    }

    public NPath ChangeFilename(string newFilename) =>
	    newFilename == "" ? Parent : Parent.Combine(newFilename);
    public NPath ChangeFilenameOnly(string filenameOnly) =>
        ChangeFilename(filenameOnly).ChangeExtension(Extension);
    public NPath ChangeFilenameOnly(Func<string, string> filenameOnlyModifier) =>
        ChangeFilenameOnly(filenameOnlyModifier(FileNameWithoutExtension));

    public NPath MakeRelative() =>
        IsRelative ? this : RelativeTo(CurrentDirectory);
    public NPath MakeRelative(NPath relativeTo) =>
        IsRelative ? this : RelativeTo(relativeTo);

    // todo:
    //   * probably want to escape things besides just space (see https://www.gnu.org/savannah-checkouts/gnu/bash/manual/bash.html#Quoting)
    //   * unix should use single quote rather than double, to avoid interpolation kicking in (or $ should be escaped too)
    public string ToCliArgString(SlashMode slashMode = SlashMode.Forward) =>
        ToString().Contains(' ') ? InQuotes(slashMode) : ToString(slashMode);

    public NPath MustExist()
    {
        if (!FileExists() && !DirectoryExists())
            throw new FileNotFoundException("File or directory expected to exist: " + this);

        return this;
    }

    public IEnumerable<NPath> SelfAndRecursiveParents
    {
        get
        {
            for (var candidate = this;; candidate = candidate.Parent)
            {
                yield return candidate;

                if (candidate.IsRoot || candidate._path == ".")
                    yield break;
            }
        }
    }

    // TODO: align this with SelfOrParentContaining..
    public NPath? TryFindFileInSelfAndParents(string filename)
    {
        var subPath = filename.ToNPath();
        return SelfAndRecursiveParents.FirstOrDefault(p => p.FileExists(subPath))?.Combine(subPath);
    }

    public NPath? SelfOrParentContaining(NPath needle, bool returnAppended = false)
    {
        var test = Combine(needle);
        if (test.Exists())
            return returnAppended ? test : this;

        return ParentContaining(needle, returnAppended);
    }

    public NPath FindFileInSelfAndParents(string filename) =>
        TryFindFileInSelfAndParents(filename)
        ?? throw new FileNotFoundException($"Could not find file in ancestry: {filename} (search from '{this}')");

    // TODO: there is a difference between windows and linux (and probably mac) here. if the dir does not exist,
    // the windows driver will return an empty array. linux will throw DirectoryNotFoundException.
    // make them work the same and get rid of "SafeDirectories".
    public NPath[] SafeDirectories(bool recurse = false) =>
        DirectoryExists() ? Directories(recurse) : [];
    public NPath[] SafeDirectories(string filter, bool recurse = false) =>
        DirectoryExists() ? Directories(filter, recurse) : [];

    public Stream OpenReadShared() =>
        File.Open(ToString(SlashMode.Native), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    public Stream OpenReadWriteShared() =>
        File.Open(ToString(SlashMode.Native), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
    public StreamReader OpenReaderShared() =>
        new(OpenReadShared());

    public Match RegexContentsMatch(string rxPattern) =>
        ReadAllText().RegexMatch(rxPattern);
    public Match RegexContentsMatch(Regex rx) =>
        ReadAllText().RegexMatch(rx);
    public IReadOnlyList<Match> RegexContentsMatches(string rxPattern) =>
        ReadAllText().RegexMatches(rxPattern);
    public IReadOnlyList<Match> RegexContentsMatches(Regex rx) =>
        ReadAllText().RegexMatches(rx);

    public string ToDisplayString(NPath? tryRelativeTo)
    {
        var relPath = tryRelativeTo != null ? MakeRelative(tryRelativeTo) : this;
        if (relPath.IsRelative)
        {
            var str = relPath.TildeCollapse().ToString(SlashMode.Forward);

            // starts to get really unreadable with more than this many
            if (!str.StartsWith("../../../../", StringComparison.Ordinal))
                return str;
        }

        return TildeCollapse().ToString(SlashMode.Forward);
    }
}
