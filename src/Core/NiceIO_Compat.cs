#nullable disable

using System.Text;

namespace OkTools.Core;

// TODO: migrate away from these shims

partial class NPath
{
	// TODO: migrate away from these shims
	public FileInfo FileInfo => new(ToString(SlashMode.Native));
	public string[] Elements => new OldNPath(this).Elements;
	public string ExtensionWithDot
	{
		get
		{
			var ext = Extension;
			if (ext != "")
				ext = '.' + ext;
			return ext;
		}
	}
	public static NPath ProgramFilesDirectory => new(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
	public static NPath RoamingAppDataDirectory => new(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
	public static NPath LocalAppDataDirectory => new(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
	public static NPath SystemTempDirectory => new(Path.GetTempPath());
}

readonly struct OldNPath
{
	public readonly string[] Elements;
	public readonly bool IsRelative;
	public readonly string DriveLetter;

	public static implicit operator NPath(OldNPath old) =>
		new(old.ToString());

	public OldNPath(string path)
	{
        ArgumentNullException.ThrowIfNull(path);

        path = ParseDriveLetter(path, out DriveLetter);

		if (path == "/")
		{
			IsRelative = false;
			Elements = Array.Empty<string>();
		}
		else
		{
			var split = path.Split('/', '\\');

			IsRelative = DriveLetter == null && IsRelativeFromSplitString(split);

			Elements = ParseSplitStringIntoElements(split.Where(s => s.Length > 0).ToArray());
		}
	}

	public OldNPath(string[] elements, bool isRelative, string driveLetter)
	{
		Elements = elements;
		IsRelative = isRelative;
		DriveLetter = driveLetter;
	}

	static string ParseDriveLetter(string path, out string driveLetter)
	{
		if (path.Length >= 2 && path[1] == ':')
		{
			driveLetter = path[0].ToString();
			return path.Substring(2);
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

	string[] ParseSplitStringIntoElements(IEnumerable<string> inputs)
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
				if (!IsRelative)
					throw new ArgumentException("You cannot create a path that tries to .. past the root");
			}
			stack.Add(input);
		}
		return stack.ToArray();
	}

	static bool HasNonDotDotLastElement(List<string> stack)
	{
		return stack.Count > 0 && stack[^1] != "..";
	}

	public override string ToString()
	{
		return ToString(SlashMode.Native);
	}

	public bool IsRoot => Elements.Length == 0 && !IsRelative;

	static char Slash(SlashMode slashMode)
	{
		return slashMode switch
		{
			SlashMode.Backward => '\\',
			SlashMode.Forward => '/',
			_ => Path.DirectorySeparatorChar
		};
	}

	public string ToString(SlashMode slashMode)
	{
		// Check if it's linux root /
		if (IsRoot && string.IsNullOrEmpty(DriveLetter))
			return Slash(slashMode).ToString();

		if (IsRelative && Elements.Length == 0)
			return ".";

		var sb = new StringBuilder();
		if (DriveLetter != null)
		{
			sb.Append(DriveLetter);
			sb.Append(':');
		}
		if (!IsRelative)
			sb.Append(Slash(slashMode));
		var first = true;
		foreach (var element in Elements)
		{
			if (!first)
				sb.Append(Slash(slashMode));

			sb.Append(element);
			first = false;
		}
		return sb.ToString();
	}
}
