namespace OkTools.Unity;

[PublicAPI]
public sealed class UnityToolchain
{
    readonly NPath _editorExePath, _monoDllPath;

    public string Path => NPath;
    internal NPath NPath => _editorExePath.Parent;
    public readonly UnityVersion Version;
    public readonly UnityToolchainOrigin Origin;

    // editor
    public string EditorExePath => _editorExePath;
    public readonly UnityEditorBuildConfig EditorBuildConfig;

    // mono
    public string MonoDllPath => _monoDllPath;
    public readonly MonoBuildConfig MonoBuildConfig;

    // TODO: was mono built locally or part of the distro? maybe check its pdb path
    // TODO: detect installed modules (mac, il2ccp, etc. - see C:\Users\scott\AppData\Roaming\UnityHub\releases.json)
    // TODO: detect architecture and platform

    UnityToolchain(NPath unityEditorExePath, UnityToolchainOrigin? origin)
    {
        _editorExePath = unityEditorExePath.FileMustExist();
        _monoDllPath = NPath.Combine(UnityConstants.MonoDllRelativeNPath).FileMustExist();
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

        // we don't store buildconfig in VERSIONINFO, so use hard coded sizes for now
        // TODO: look into UnityEditor.Unsupported.IsNativeCodeBuiltInReleaseMode()..is there any way to extract this statically?
        EditorBuildConfig = (_editorExePath.FileInfo.Length / (1024.0 * 1024)) switch
        {
            >  60 and < 150 => UnityEditorBuildConfig.Release,
            > 300 and < 400 => UnityEditorBuildConfig.Debug,

            var sizeMb => throw new InvalidDataException(
                $"Unexpected size of {_editorExePath} ({sizeMb}MB) need to revise detection bounds for Editor build config")
        };

        // same deal as editor buildconfig regarding hard coded size matching
        MonoBuildConfig = (_monoDllPath.FileInfo.Length / (1024.0 * 1024)) switch
        {
            > 4 and < 7.5 => MonoBuildConfig.Release,
            > 9 and <  11 => MonoBuildConfig.Debug,

            var sizeMb => throw new InvalidDataException(
                $"Unexpected size of {_editorExePath} ({sizeMb}MB) need to revise detection bounds for Mono build config")
        };
    }

    public override string ToString()
    {
        return $"{NPath}: {Version} ({EditorBuildConfig}, {Origin})";
    }

    /// <summary>
    /// Look in the given path for a Unity toolchain and return it if found.
    /// </summary>
    /// <param name="pathToUnityBuild">The path to look for a Unity toolchain. Can point at the toolchain folder or at
    /// a unity.exe in the folder.</param>
    /// <returns>If a toolchain is found, returns a `UnityToolchain` object, otherwise null. May throw if it finds
    /// an incomplete or corrupt toolchain.</returns>
    public static UnityToolchain? TryCreateFromPath(string pathToUnityBuild, UnityToolchainOrigin? origin = null) =>
        TryCreateFromPath(pathToUnityBuild.ToNPath(), origin);
    internal static UnityToolchain? TryCreateFromPath(NPath pathToUnityBuild, UnityToolchainOrigin? origin = null)
    {
        if (!pathToUnityBuild.FileName.EqualsIgnoreCase(UnityConstants.UnityExeName))
            pathToUnityBuild = pathToUnityBuild.Combine(UnityConstants.UnityExeName);

        return pathToUnityBuild.FileExists() ? new UnityToolchain(pathToUnityBuild, origin) : null;
    }
}

[PublicAPI]
public enum UnityEditorBuildConfig
{
    Debug, Release
}

[PublicAPI]
public enum MonoBuildConfig
{
    Debug, Release
}

[PublicAPI]
public enum UnityToolchainOrigin
{
    Unknown, LocallyBuilt, UnityDownloader, UnityHub, ManuallyInstalled
}
