namespace OkTools.Unity;

[PublicAPI]
public enum UnityEditorBuildConfig
{
    Debug, Release
}

[PublicAPI]
public enum MonoBuildConfig
{
    Missing, Debug, Release
}

[PublicAPI]
public enum UnityToolchainOrigin
{
    LocallyBuilt, UnityDownloader, ManuallyInstalled, UnityHub, Unknown
}

// TODO: support toolchain aliases that can be assigned through the ini config chain. so i can put a .okunity in my
// fpssample folder with aliases to "gamedev" which points at a certain folder.
//
// also: preferences for which toolchain to use when discovering debug vs release, locally built vs not, or when we have
// two same versions but different hashes (go by branch name).
//
// maybe toolchain could be something like "gamedev+release?+local" (alias, prefer but not require release, and require a local build)

[PublicAPI]
public class UnityToolchain : IStructuredOutput
{
    readonly NPath _editorExePath;
    readonly NPath? _monoDllPath;

    public string Path => NPath;
    internal NPath NPath => _editorExePath.Parent;
    public readonly UnityVersion Version;
    public readonly UnityToolchainOrigin Origin;

    // editor
    public string EditorExePath => _editorExePath;
    public readonly UnityEditorBuildConfig EditorBuildConfig;

    // mono
    public string? MonoDllPath => _monoDllPath?.ToString();
    public readonly MonoBuildConfig MonoBuildConfig;

    // TODO: was mono built locally or part of the distro? maybe check its pdb path
    // TODO: detect installed modules (mac, il2ccp, etc. - see C:\Users\scott\AppData\Roaming\UnityHub\releases.json)

    UnityToolchain(NPath unityEditorExePath, UnityToolchainOrigin? origin)
    {
        _editorExePath = unityEditorExePath.MakeAbsolute().FileMustExist();
        _monoDllPath = NPath.Combine(UnityConstants.MonoDllRelativeNPath).MakeAbsolute();
        if (!_monoDllPath.FileExists())
            _monoDllPath = null;

        Version = UnityVersion.FromUnityExe(_editorExePath);

        // TODO: ideally we'd be detecting whether it's hub-managed or manually-installed _here_, without needing to be
        // told by the caller, which may not match up. the user may say "no defaults" and then set the hub root as a
        // manual path. this would require some amount of caching of the hub json and installer registry queries to
        // avoid redoing it for every single toolchain.

        if (origin == null)
        {
            if (NPath.Combine(".unity-downloader-meta.yml").FileExists())
                Origin = UnityToolchainOrigin.UnityDownloader;
            else if (NPath.ParentContaining("build.pl") is not null)
                Origin = UnityToolchainOrigin.LocallyBuilt;
            else
                Origin = UnityToolchainOrigin.Unknown;
        }
        else
            Origin = origin.Value;

        // we don't store buildconfig in VERSIONINFO. you can call UnityEditor.Unsupported.IsNativeCodeBuiltInReleaseMode()
        // but this is an icall into a native function that just returns UNITY_RELEASE.
        //
        // for a while i was tracking size ranges (as aras sort-of-recommended) but that isn't stable.
        //
        // now what i do is take advantage of the fact that the bug reporter is also built with debug/release just like
        // unity, and unlike unity, it uses different dll names when built in debug. so if you have a debug unity with a
        // release bug reporter, this will report the wrong thing. best i can do for now.. :/
        {
            var bugReporterDir = _editorExePath.Parent.Combine("BugReporter").DirectoryMustExist();
            var hasDebug = bugReporterDir.Combine("Qt5Cored.dll").FileExists();
            var hasRelease = bugReporterDir.Combine("Qt5Core.dll").FileExists();

            if (hasDebug && !hasRelease)
                EditorBuildConfig = UnityEditorBuildConfig.Debug;
            else if (!hasDebug && hasRelease)
                EditorBuildConfig = UnityEditorBuildConfig.Release;
            else
                throw new InvalidDataException("Could not determine editor build config using presence of BugReporter/Qt5Core*.dll");
        }

        // same deal as editor buildconfig regarding hard coded size matching
        if (_monoDllPath != null)
        {
            MonoBuildConfig = (_monoDllPath.FileInfo.Length / (1024.0 * 1024)) switch
            {
                > 4 and < 7.8 => MonoBuildConfig.Release,
                > 9 and <  11 => MonoBuildConfig.Debug,

                var sizeMb => throw new InvalidDataException(
                    $"Unexpected size of {_editorExePath} ({sizeMb:0.0}MB) need to revise detection bounds for Mono build config")
            };
        }
        else
            MonoBuildConfig = MonoBuildConfig.Missing;
    }

    public override string ToString() => $"{Version} ({EditorBuildConfig}, {Origin}):\n  {NPath}";

    public object Output(StructuredOutputLevel level, bool debug)
    {
        var lastWrite = GetInstallTime();

        var output = Expando.From(new
        {
            Path,
            Version = Version.ToString(),
            EditorBuildConfig,
            Installed = lastWrite.ToNiceAge(true),
            Origin,
        });

        if (level >= StructuredOutputLevel.Normal)
            Expando.Add(output, new { MonoDllPath, MonoBuildConfig });
        if (level >= StructuredOutputLevel.Detailed)
            Expando.Add(output, new { VersionFull = Version, EditorExePath, EditorExeLastWrite = lastWrite });

        // TODO: if git discovered, add info like with UnityProject
        // TODO: check whether it's in a folder that has a version in it, and if so, whether it matches the actual version

        return output;
    }

    public DateTime GetInstallTime()
    {
        return _editorExePath.FileInfo.LastWriteTime;
    }

    /// <summary>
    /// Look in the given path for a Unity toolchain and return it if found.
    /// </summary>
    /// <param name="pathToUnityToolchain">The path to look for a Unity toolchain. Can point at the toolchain folder or at
    /// a unity.exe in the folder.</param>
    /// <param name="origin">Optional origin to assign, otherwise it will try to autodetect.</param>
    /// <returns>If a toolchain is found, returns a `UnityToolchain` object, otherwise null. May throw if it finds
    /// an incomplete or corrupt toolchain.</returns>
    public static UnityToolchain? TryCreateFromPath(string pathToUnityToolchain, UnityToolchainOrigin? origin = null) =>
        TryCreateFromPath(pathToUnityToolchain.ToNPath(), origin);
    internal static UnityToolchain? TryCreateFromPath(NPath pathToUnityToolchain, UnityToolchainOrigin? origin = null)
    {
        if (!pathToUnityToolchain.FileName.EqualsIgnoreCase(UnityConstants.UnityExeName))
            pathToUnityToolchain = pathToUnityToolchain.Combine(UnityConstants.UnityExeName);

        return pathToUnityToolchain.FileExists() ? new UnityToolchain(pathToUnityToolchain, origin) : null;
    }
}
