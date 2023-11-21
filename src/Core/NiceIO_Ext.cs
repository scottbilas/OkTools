#nullable disable

namespace OkTools.Core;

// TODO XMLDoc :D

partial class NPath
{
    public static bool operator <(NPath left, NPath right) =>
        left is null ? right is not null : left.CompareTo(right) < 0;
    public static bool operator <=(NPath left, NPath right) =>
        left is null || left.CompareTo(right) <= 0;
    public static bool operator >(NPath left, NPath right) =>
        !(left <= right);
    public static bool operator >=(NPath left, NPath right) =>
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

    public NPath ParentContaining(string needle, bool returnAppended) =>
	    ParentContaining(needle.ToNPath(), returnAppended);

    public NPath ParentContaining(NPath needle, bool returnAppended)
    {
	    var found = ParentContaining(needle);
	    if (found != null && returnAppended)
		    found = found.Combine(needle);

	    return found;
    }

    // TODO: make this actually stream
    public IEnumerable<string> ReadLines()
    {
	    foreach (var line in this.ReadAllLines())
		    yield return line;
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

    public NPath Move(string dest, bool overwrite)
    {
	    return Move(new NPath(dest), overwrite);
    }

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
		    //FileSystem.Active.File_Move(this, dest, overwrite); TODO UPDATE
		    File.Move(ToString(SlashMode.Native), dest.ToString(SlashMode.Native), overwrite);
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
