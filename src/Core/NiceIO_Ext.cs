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
                if (File.Exists(srcNativePath))
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
}
