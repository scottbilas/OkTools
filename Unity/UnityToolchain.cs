namespace OkTools.Unity;

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

[PublicAPI]
public class UnityToolchain : IStructuredOutput
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
        _editorExePath = unityEditorExePath.MakeAbsolute().FileMustExist();
        _monoDllPath = NPath.Combine(UnityConstants.MonoDllRelativeNPath).MakeAbsolute().FileMustExist();
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

    public override string ToString() => $"{Version} ({EditorBuildConfig}, {Origin}):\n  {NPath}";

    public object Output(StructuredOutputLevel level, bool debug)
    {
        var lastWrite = _editorExePath.FileInfo.LastWriteTime;

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
