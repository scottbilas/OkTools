// The MIT License(MIT)
// =====================
//
// Copyright © `2015-2017` `Lucas Meijer`
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the “Software”), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.

using System.Diagnostics;
using System.Text;

// TODO: eliminate these
#pragma warning disable CA1036
#pragma warning disable CA1304
#pragma warning disable CA1310

namespace NiceIO
{
    // TODO: either disallow or properly handle \\server\share\etc type paths
    // TODO: either disallow or properly handle \\?\C:\path\to\file type paths
    // TODO: either disallow or properly handle c:abc paths (currently converts to c:\abc)
    // TODO: consider whether is a bug that new NPath("") and new NPath(".") have same ToString() but different state
    // TODO: handle MAX_PATH as per https://stackoverflow.com/a/57624626/14582
    // TODO: proper full set of equatable and comparable interfaces
    // TODO: case-insensitive NPath comparer
    // TODO: think about case sensitivity across platforms, defaults we might use, how to override
    // TODO: get rid of string[] _elements and keep it as a simple string. make NPath immutable. (ok but how to Combine() without an alloc every time? linked list to "next"? nah)
    // TODO: defer string-path cleanup until actually needed (like on a ToString or GetHashCode). we can handle mixed / \ for many operations like .Filename and .Parent
    // TODO: make it a struct with (RO)Span support, look into `unsafe` "fixed char _path[260]" type thing..probably not tho..NPath doesn't live long before needing to become a proper string
    // TODO: look into nuget alternatives and get rid of NiceIO entirely

    [PublicAPI]
    [DebuggerDisplay("{FileName} ({ToString()})")]
    public class NPath : IEquatable<NPath>, IComparable
    {
        static readonly StringComparison s_pathStringComparison = IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        readonly string[] _elements;
        readonly bool _isRelative;
        readonly string? _driveLetter;

        #region construction

        public NPath(string path)
        {
            ArgumentNullException.ThrowIfNull(path);

            path = ParseDriveLetter(path, out _driveLetter);

            if (path == "/")
            {
                _isRelative = false;
                _elements = Array.Empty<string>();
            }
            else
            {
                var split = path.Split('/', '\\');

                _isRelative = _driveLetter == null && IsRelativeFromSplitString(split);
                _elements = ParseSplitStringIntoElements(split.Where(s => s.Length > 0)).ToArray();
            }
        }

        NPath(IEnumerable<string> elements, bool isRelative, string? driveLetter)
        {
            _elements = elements.ToArray();
            _isRelative = isRelative;
            _driveLetter = driveLetter;
        }

        List<string> ParseSplitStringIntoElements(IEnumerable<string> inputs)
        {
            var stack = new List<string>();

            foreach (var input in inputs.Where(input => input.Length != 0))
            {
                if (input == ".")
                {
                    if ((stack.Count > 0) && (stack.Last() != "."))
                        continue;
                }
                else if (input == "..")
                {
                    if (HasNonDotDotLastElement(stack))
                    {
                        stack.RemoveAt(stack.Count - 1);
                        continue;
                    }
                    if (!_isRelative)
                        throw new ArgumentException("You cannot create a path that tries to .. past the root");
                }
                stack.Add(input);
            }
            return stack;
        }

        static bool HasNonDotDotLastElement(List<string> stack)
        {
            return stack.Count > 0 && stack[^1] != "..";
        }

        static string ParseDriveLetter(string path, out string? driveLetter)
        {
            if (path.Length >= 2 && path[1] == ':')
            {
                driveLetter = path[0].ToString();
                return path[2..];
            }

            driveLetter = null;
            return path;
        }

        static bool IsRelativeFromSplitString(string[] split)
        {
            if (split.Length < 2)
                return true;

            return split[0].Length != 0 || !split.Any(s => s.Length > 0);
        }

        public NPath Combine(params string[] append)
        {
            return Combine(append.AsEnumerable());
        }

        public NPath Combine(IEnumerable<string> append)
        {
            return Combine(append.Select(a => new NPath(a)));
        }

        public NPath Combine(params NPath[] append)
        {
            return Combine(append.AsEnumerable());
        }

        public NPath Combine(IEnumerable<NPath> append)
        {
            // TODO: throw if any are empty

            return new NPath(
                ParseSplitStringIntoElements(_elements.Concat(append.SelectMany(
                    p => p.IsRelative
                        ? p._elements
                        : throw new ArgumentException("You cannot .Combine a non-relative path")))),
                _isRelative,
                _driveLetter);
        }

        public NPath Parent
        {
            get
            {
                if (_elements.Length == 0)
                    throw new InvalidOperationException ("Parent is called on an empty path");

                var newElements = _elements.Take (_elements.Length - 1);

                return new NPath (newElements, _isRelative, _driveLetter);
            }
        }

        /// <summary>
        /// Split path at the given element index, returning two paths that, if combined, result in the original path.
        /// The subPath begins at the split index, and must be valid within the range of [0,Depth].
        /// </summary>
        public (NPath basePath, NPath subPath) SplitAtElement(int elementIndex)
        {
            if (elementIndex < 0 || elementIndex >= Depth)
                throw new ArgumentOutOfRangeException(nameof(elementIndex), $"Out of range 0 <= {elementIndex} < {Depth}");

            return (
                new NPath(_elements.Take(elementIndex), _isRelative, _driveLetter),
                new NPath(_elements.Skip(elementIndex), true, null));
        }

        public NPath RelativeTo(NPath path)
        {
            if (!IsChildOf(path))
            {
                if (!IsRelative && !path.IsRelative && _driveLetter != path._driveLetter)
                    throw new ArgumentException("Path.RelativeTo() was invoked with two paths that are on different volumes. invoked on: " + ToString() + " asked to be made relative to: " + path);

                NPath? commonParent = null;
                foreach (var parent in RecursiveParents)
                {
                    commonParent = path.RecursiveParents.FirstOrDefault(otherParent => otherParent == parent);

                    if (commonParent != null)
                        break;
                }

                if (commonParent == null)
                    throw new ArgumentException("Path.RelativeTo() was unable to find a common parent between " + ToString() + " and " + path);

                if (IsRelative && path.IsRelative && commonParent.IsEmpty())
                    throw new ArgumentException("Path.RelativeTo() was invoked with two relative paths that do not share a common parent.  Invoked on: " + ToString() + " asked to be made relative to: " + path);

                var depthDiff = path.Depth - commonParent.Depth;
                return new NPath(Enumerable.Repeat("..", depthDiff).Concat(_elements.Skip(commonParent.Depth)).ToArray(), true, null);
            }

            return new NPath(_elements.Skip(path._elements.Length).ToArray(), true, null);
        }

        public NPath ChangeExtension(string extension)
        {
            ThrowIfRoot();

            var newElements = (string[])_elements.Clone();
            newElements[^1] = Path.ChangeExtension(_elements[^1], WithDot(extension));
            if (extension == string.Empty)
                newElements[^1] = newElements[^1].TrimEnd('.');
            return new NPath(newElements, _isRelative, _driveLetter);
        }

        public NPath ChangeFilename(string filename)
        {
            ThrowIfRoot();

            string[] newElements;
            if (filename == string.Empty)
            {
                newElements = new string[_elements.Length - 1];
                Array.Copy(_elements, newElements, newElements.Length);
            }
            else
            {
                newElements = new string[_elements.Length];
                Array.Copy(_elements, newElements, newElements.Length - 1);
                newElements[^1] = filename;
            }

            return new NPath(newElements, _isRelative, _driveLetter);
        }
        #endregion construction

        #region inspection

        public bool IsRelative
        {
            get { return _isRelative; }
        }

        public string FileName
        {
            get
            {
                ThrowIfRoot();

                return _elements.Last();
            }
        }

        public string FileNameWithoutExtension
        {
            get { return Path.GetFileNameWithoutExtension (FileName); }
        }

        public FileInfo FileInfo
        {
            get { return new FileInfo(this); }
        }

        public IReadOnlyList<string> Elements
        {
            get { return _elements; }
        }

        // TODO: throw on relative
        public string? DriveLetter
        {
            get { return _driveLetter; }
		}

		public int Depth
		{
			get { return _elements.Length; }
		}

        public bool Exists(string append = "")
        {
            return Exists(new NPath(append));
        }

        public bool Exists(NPath append)
        {
            return FileExists(append) || DirectoryExists(append);
        }

        public bool DirectoryExists(string append = "")
        {
            return DirectoryExists(new NPath(append));
        }

        public bool DirectoryExists(NPath append)
        {
            return Directory.Exists(Combine(append).ToString());
        }

        public bool FileExists(string append = "")
        {
            return FileExists(new NPath(append));
        }

        public bool FileExists(NPath append)
        {
            return File.Exists(Combine(append).ToString());
        }

        public string ExtensionWithDot
        {
            get
            {
                if (IsRoot)
                    throw new ArgumentException("A root directory does not have an extension");

                var last = _elements.Last();
                var index = last.LastIndexOf('.');
                if (index < 0) return String.Empty;
                return last.Substring(index);
            }
        }

        public string ExtensionWithoutDot // TODO: rename to just Extension
        {
            get
            {
                if (IsRoot)
                    throw new ArgumentException("A root directory does not have an extension");

                var last = _elements.Last();
                var index = last.LastIndexOf('.');
                if (index < 0) return String.Empty;
                return last.Substring(index + 1);
            }
        }
        public string InQuotes()
        {
            return "\"" + ToString() + "\"";
        }

        public string InQuotes(SlashMode slashMode)
        {
            return "\"" + ToString(slashMode) + "\"";
        }

        [DebuggerStepThrough]
        public override string ToString()
        {
            return ToString(SlashMode.Native);
        }

        public string ToString(SlashMode slashMode)
        {
            // Check if it's linux root /
            if (IsRoot && string.IsNullOrEmpty(_driveLetter))
                return Slash(slashMode).ToString();

            if (_isRelative && _elements.Length == 0)
                return ".";

            var sb = new StringBuilder();
            if (_driveLetter != null)
            {
                sb.Append(_driveLetter);
                sb.Append(':');
            }
            if (!_isRelative)
                sb.Append(Slash(slashMode));
            var first = true;
            foreach (var element in _elements)
            {
                if (!first)
                    sb.Append(Slash(slashMode));

                sb.Append(element);
                first = false;
            }
            return sb.ToString();
        }

        [DebuggerStepThrough]
        public static implicit operator string(NPath path)
        {
            return path.ToString();
        }

        static char Slash(SlashMode slashMode)
        {
            switch (slashMode)
            {
                case SlashMode.Backward:
                    return '\\';
                case SlashMode.Forward:
                    return '/';
                default:
                    return Path.DirectorySeparatorChar;
            }
        }

        public override bool Equals(object? obj)
        {
            return obj is NPath path && Equals(path);
        }

        public bool Equals(NPath? other)
        {
            if (other == null)
                return false;

            if (other._isRelative != _isRelative)
                return false;

            if (!string.Equals(other._driveLetter, _driveLetter, s_pathStringComparison))
                return false;

            if (other._elements.Length != _elements.Length)
                return false;

            for (var i = 0; i != _elements.Length; i++)
                if (!string.Equals(other._elements[i], _elements[i], s_pathStringComparison))
                    return false;

            return true;
        }

        public static bool operator ==(NPath? a, NPath? b)
        {
            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(a, b))
                return true;

            // If one is null, but not both, return false.
            if (a is null || b is null)
                return false;

            // Return true if the fields match:
            return a.Equals(b);
        }

        public override int GetHashCode()
        {
            // TODO: use hash builder
            unchecked
            {
                int hash = 17;
                // Suitable nullity checks etc, of course :)
                hash = hash * 23 + _isRelative.GetHashCode();
                foreach (var element in _elements)
                    hash = hash * 23 + element.GetHashCode();
                if (_driveLetter != null)
                    hash = hash * 23 + _driveLetter.GetHashCode();
                return hash;
            }
        }

        public int CompareTo(object? obj)
        {
            // TODO: wat? A < B if B==null?
            if (obj == null)
                return -1;

            return string.Compare(ToString(), ((NPath)obj).ToString(), s_pathStringComparison);
        }

        public static bool operator !=(NPath? a, NPath? b)
        {
            return !(a == b);
        }

        public bool HasExtension(params string[] extensions)
        {
            var extensionWithDotLower = ExtensionWithDot.ToLowerInvariant();
            return extensions.Any(e => WithDot(e).ToLowerInvariant() == extensionWithDotLower);
        }

        static string WithDot(string extension)
        {
            return extension.StartsWith(".") ? extension : "." + extension;
        }

        bool IsEmpty()
        {
            return _elements.Length == 0;
        }

        public bool IsRoot
        {
            get { return _elements.Length == 0 && !_isRelative; }
        }

        #endregion inspection

        #region directory enumeration

        public IEnumerable<NPath> Files(string filter, bool recurse = false)
        {
			return Directory.GetFiles(ToString(), filter, recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).Select(s => new NPath(s));
        }

        public IEnumerable<NPath> Files(bool recurse = false)
        {
            return Files("*", recurse);
        }

        public IEnumerable<NPath> Contents(string filter, bool recurse = false)
        {
            return Files(filter, recurse).Concat(Directories(filter, recurse));
        }

        public IEnumerable<NPath> Contents(bool recurse = false)
        {
            return Contents("*", recurse);
        }

        public IEnumerable<NPath> Directories(string filter, bool recurse = false)
        {
			return Directory.GetDirectories(ToString(), filter, recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).Select(s => new NPath(s));
        }

        public IEnumerable<NPath> Directories(bool recurse = false)
        {
            return Directories("*", recurse);
        }

        public NPath TildeExpand()
        {
            // implementing only the most basic part of https://www.gnu.org/software/bash/manual/html_node/Tilde-Expansion.html

            if (!IsRelative || _elements.FirstOrDefault() != "~")
                return this;

            return HomeDirectory.Combine(_elements.Skip(1));
        }

        public NPath TildeCollapse()
        {
            var thisAbs = MakeAbsolute();
            var homeDir = HomeDirectory;

            if (!thisAbs.IsChildOf(HomeDirectory))
                return this;

            var relative = thisAbs.RelativeTo(homeDir);
            if (relative.Depth == 0)
                return "~";

            return new NPath("~").Combine(relative);
        }

        #endregion

        #region filesystem writing operations
        public NPath CreateFile()
        {
            ThrowIfRelative();
            ThrowIfRoot();
            EnsureParentDirectoryExists();
	        File.WriteAllBytes(ToString(), Array.Empty<byte>());
            return this;
        }

        public NPath CreateFile(string file)
        {
            return CreateFile(new NPath(file));
        }

        public NPath CreateFile(NPath file)
        {
            if (!file.IsRelative)
                throw new ArgumentException("You cannot call CreateFile() on an existing path with a non relative argument");
            return Combine(file).CreateFile();
        }

        public NPath CreateDirectory()
        {
            ThrowIfRelative();

            if (IsRoot)
                throw new NotSupportedException("CreateDirectory is not supported on a root level directory because it would be dangerous:" + ToString());

            Directory.CreateDirectory(ToString());
            return this;
        }

        public NPath CreateDirectory(string directory)
        {
            return CreateDirectory(new NPath(directory));
        }

        public NPath CreateDirectory(NPath directory)
        {
            if (!directory.IsRelative)
                throw new ArgumentException("Cannot call CreateDirectory with an absolute argument");

            return Combine(directory).CreateDirectory();
        }

        public NPath Copy(string dest)
        {
            return Copy(new NPath(dest));
        }

        public NPath? Copy(string dest, Func<NPath, bool> fileFilter)
        {
            return Copy(new NPath(dest), fileFilter);
        }

        public NPath Copy(NPath dest)
        {
            return Copy(dest, _ => true)!;
        }

        public NPath? Copy(NPath dest, Func<NPath, bool> fileFilter)
        {
            ThrowIfRelative();
            if (dest.IsRelative)
                dest = Parent.Combine(dest);

            if (dest.DirectoryExists())
                return CopyWithDeterminedDestination(dest.Combine(FileName), fileFilter);

            return CopyWithDeterminedDestination (dest, fileFilter);
        }

        public NPath MakeAbsolute()
        {
            if (!IsRelative)
                return this;

            return NPath.CurrentDirectory.Combine (this);
        }

        NPath? CopyWithDeterminedDestination(NPath absoluteDestination, Func<NPath,bool> fileFilter)
        {
            if (absoluteDestination.IsRelative)
                throw new ArgumentException ("absoluteDestination must be absolute");

            if (FileExists())
            {
                if (!fileFilter(absoluteDestination))
                    return null;

                absoluteDestination.EnsureParentDirectoryExists();

                File.Copy(ToString(), absoluteDestination.ToString(), true);
                return absoluteDestination;
            }

            if (DirectoryExists())
            {
                absoluteDestination.EnsureDirectoryExists();
                foreach (var thing in Contents())
                    thing.CopyWithDeterminedDestination(absoluteDestination.Combine(thing.RelativeTo(this)), fileFilter);
                return absoluteDestination;
            }

            throw new ArgumentException("Copy() called on path that doesnt exist: " + ToString());
        }

        public void Delete(DeleteMode deleteMode = DeleteMode.Normal)
        {
            ThrowIfRelative();

            if (IsRoot)
                throw new NotSupportedException("Delete is not supported on a root level directory because it would be dangerous:" + ToString());

            if (FileExists())
                File.Delete(ToString());
            else if (DirectoryExists())
                try
                {
                    Directory.Delete(ToString(), true);
                }
                catch (IOException)
                {
                    if (deleteMode == DeleteMode.Normal)
                        throw;
                }
            else
                throw new InvalidOperationException("Trying to delete a path that does not exist: " + ToString());
        }

        public void DeleteIfExists(DeleteMode deleteMode = DeleteMode.Normal)
        {
            ThrowIfRelative();

            if (FileExists() || DirectoryExists())
                Delete(deleteMode);
        }

        public NPath DeleteContents()
        {
            ThrowIfRelative();

            if (IsRoot)
                throw new NotSupportedException("DeleteContents is not supported on a root level directory because it would be dangerous:" + ToString());

            if (FileExists())
                throw new InvalidOperationException("It is not valid to perform this operation on a file");

            if (DirectoryExists())
            {
                try
                {
                    Files().Delete();
                    Directories().Delete();
                }
                catch (IOException)
                {
                    if (Files(true).Any())
                        throw;
                }

                return this;
            }

            return EnsureDirectoryExists();
        }

        public static NPath CreateTempDirectory(string myprefix)
        {
            var random = new Random();
            while (true)
            {
                var candidate = new NPath(Path.GetTempPath() + "/" + myprefix + "_" + random.Next());
                if (!candidate.Exists())
                    return candidate.CreateDirectory();
            }
        }

        public NPath Move(string dest)
        {
            return Move(new NPath(dest));
        }

        public NPath Move(NPath dest)
        {
            ThrowIfRelative();

            if (IsRoot)
                throw new NotSupportedException("Move is not supported on a root level directory because it would be dangerous:" + ToString());

            if (dest.IsRelative)
                return Move(Parent.Combine(dest));

            if (dest.DirectoryExists())
                return Move(dest.Combine(FileName));

            if (FileExists())
            {
                dest.EnsureParentDirectoryExists();
                File.Move(ToString(), dest.ToString());
                return dest;
            }

            if (DirectoryExists())
            {
                Directory.Move(ToString(), dest.ToString());
                return dest;
            }

            throw new ArgumentException("Move() called on a path that doesn't exist: " + ToString());
        }

        #endregion

        #region special paths

        public static NPath CurrentDirectory => new(Directory.GetCurrentDirectory());
        public static NPath HomeDirectory => new(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        public static NPath ProgramFilesDirectory => new(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        public static NPath RoamingAppDataDirectory => new(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        public static NPath LocalAppDataDirectory => new(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        public static NPath SystemTempDirectory => new(Path.GetTempPath());

        #endregion

        public void ThrowIfRelative()
        {
            if (_isRelative)
                throw new ArgumentException($"You are attempting an operation on a path that requires an absolute path, but the path is relative ({this})");
        }

        public void ThrowIfRoot()
        {
            if (IsRoot)
                throw new ArgumentException("You are attempting an operation that is not valid on a root level directory");
        }

        public NPath EnsureDirectoryExists(string append = "")
        {
            return EnsureDirectoryExists(new NPath(append));
        }

        public NPath EnsureDirectoryExists(NPath append)
        {
            var combined = Combine(append);
            if (combined.DirectoryExists())
                return combined;
            combined.EnsureParentDirectoryExists();
            combined.CreateDirectory();
            return combined;
        }

        public NPath EnsureParentDirectoryExists()
        {
            var parent = Parent;
            parent.EnsureDirectoryExists();
            return parent;
        }

        public bool IsChildOf(string potentialBasePath)
        {
            return IsChildOf(new NPath(potentialBasePath));
        }

        public bool IsChildOf(NPath potentialBasePath)
        {
            if ((IsRelative && !potentialBasePath.IsRelative) || !IsRelative && potentialBasePath.IsRelative)
                throw new ArgumentException("You can only call IsChildOf with two relative paths, or with two absolute paths");

            // If the other path is the root directory, then anything is a child of it as long as it's not a Windows path
            if (potentialBasePath.IsRoot)
            {
                if (_driveLetter != potentialBasePath._driveLetter)
                    return false;
                return true;
            }

            if (IsEmpty())
                return false;

            if (Equals(potentialBasePath))
                return true;

            return Parent.IsChildOf(potentialBasePath);
        }

        public IEnumerable<NPath> RecursiveParents
        {
            get
            {
                var candidate = this;
                while (true)
                {
                    if(candidate.IsEmpty())
                        yield break;

                    candidate = candidate.Parent;
                    yield return candidate;
                }
            }
        }

        public NPath? ParentContaining(string needle, bool returnAppended = false) =>
            ParentContaining(needle.ToNPath(), returnAppended);

        public NPath? ParentContaining(NPath needle, bool returnAppended = false)
        {
            ThrowIfRelative();

            var found = RecursiveParents.FirstOrDefault(p => p.Exists(needle));
            if (returnAppended && found != null)
                found = found.Combine(needle);

            return found;
        }

        public NPath WriteAllText(string contents)
        {
            ThrowIfRelative();
            EnsureParentDirectoryExists();
            File.WriteAllText(ToString(), contents);
            return this;
        }

        public string ReadAllText()
        {
            ThrowIfRelative();
            return File.ReadAllText(ToString());
        }

        public NPath WriteAllLines(params string[] contents)
        {
            ThrowIfRelative();
            EnsureParentDirectoryExists();
            File.WriteAllLines(ToString(), contents);
            return this;
        }

        public string[] ReadAllLines()
        {
            ThrowIfRelative();
            return File.ReadAllLines(ToString());
        }

        public IEnumerable<string> ReadLines()
        {
            ThrowIfRelative();
            return File.ReadLines(ToString());
        }

        public IEnumerable<NPath> CopyFiles(NPath destination, bool recurse, Func<NPath, bool>? fileFilter = null)
        {
            destination.EnsureDirectoryExists();
            return Files(recurse).Where(fileFilter ?? AlwaysTrue).Select(file => file.Copy(destination.Combine(file.RelativeTo(this)))).ToArray();
        }

        public IEnumerable<NPath> MoveFiles(NPath destination, bool recurse, Func<NPath, bool>? fileFilter = null)
        {
            if (IsRoot)
                throw new NotSupportedException("MoveFiles is not supported on this directory because it would be dangerous:" + ToString());

            destination.EnsureDirectoryExists();
            return Files(recurse).Where(fileFilter ?? AlwaysTrue).Select(file => file.Move(destination.Combine(file.RelativeTo(this)))).ToArray();
        }

        static bool AlwaysTrue(NPath p)
        {
            return true;
        }

        static bool IsLinux()
        {
            return Directory.Exists("/proc");
        }
        public static implicit operator NPath(string input)
        {
            return new NPath(input);
    }
	}

    public static class NPathExtensions
    {
        // note: NPath is nullable on *MustExist because of functions like ParentContaining that may return null

        public static NPath FileMustExist(this NPath? @this)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this), "Path is null; did a previous NPath operation fail?");

            if (@this.FileExists())
                return @this;

            if (@this.DirectoryExists())
                throw new FileNotFoundException($"Found directory instead of file '{@this}'", @this);

            throw new FileNotFoundException($"Could not find file '{@this}'", @this);

        }

        public static NPath DirectoryMustExist(this NPath? @this)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this), "Path is null; did a previous NPath operation fail?");

            if (@this.DirectoryExists())
                return @this;

            if (@this.FileExists())
                throw new DirectoryNotFoundException($"Found file instead of directory '{@this}'");

            throw new DirectoryNotFoundException($"Could not find directory '{@this}'");

        }

        public static IEnumerable<NPath> Copy(this IEnumerable<NPath> self, string dest)
        {
            return Copy(self, new NPath(dest));
        }

        public static IEnumerable<NPath> Copy(this IEnumerable<NPath> self, NPath dest)
        {
            if (dest.IsRelative)
                throw new ArgumentException("When copying multiple files, the destination cannot be a relative path");
            dest.EnsureDirectoryExists();
            return self.Select(p => p.Copy(dest.Combine(p.FileName))).ToArray();
        }

        public static IEnumerable<NPath> Move(this IEnumerable<NPath> self, string dest)
        {
            return Move(self, new NPath(dest));
        }

        public static IEnumerable<NPath> Move(this IEnumerable<NPath> self, NPath dest)
        {
            if (dest.IsRelative)
                throw new ArgumentException("When moving multiple files, the destination cannot be a relative path");
            dest.EnsureDirectoryExists();
            return self.Select(p => p.Move(dest.Combine(p.FileName))).ToArray();
        }

        public static int Delete(this IEnumerable<NPath> self)
        {
            var deleted = 0;
            foreach (var p in self)
            {
                p.Delete();
                ++deleted;
            }

            return deleted;
        }

        public static IEnumerable<string> InQuotes(this IEnumerable<NPath> self, SlashMode forward = SlashMode.Native)
        {
            return self.Select(p => p.InQuotes(forward));
        }

        [DebuggerStepThrough]
        public static NPath ToNPath(this string path) =>
            new(path);
        public static IEnumerable<NPath> ToNPath(this IEnumerable<string> @this) =>
            @this.Select(p => p.ToNPath());
    }

    public enum SlashMode
    {
        Native,
        Forward,
        Backward
    }

    public enum DeleteMode
    {
        Normal,
        Soft
    }


}
