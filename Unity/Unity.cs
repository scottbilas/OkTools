namespace OkTools.Unity;

[PublicAPI]
public static class UnityConstants
{
    // TODO: platform!

    public const string UnityExeName = "unity.exe";
    public const string ProjectAssetsFolderName = "Assets";
    public const string ProjectLogsFolderName = "Logs";
    public const string ProjectVersionTxtFileName = "ProjectVersion.txt";
    public const string EditorsYmlFileName = "editors.yml";

    internal static readonly NPath ProjectVersionRelativeNPath = new NPath("ProjectSettings").Combine(ProjectVersionTxtFileName);
    public static readonly string ProjectVersionRelativePath = ProjectVersionRelativeNPath;

    internal static readonly NPath ArtifactDbRelativeNPath = new("Library/ArtifactDB");
    public static readonly string ArtifactDbRelativePath = ArtifactDbRelativeNPath;

    internal static readonly NPath MonoDllRelativeNPath = "Data/MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll".ToNPath();
    public static readonly string MonoDllRelativePath = MonoDllRelativeNPath;

    internal static readonly NPath HubInstalledToolchainNPathSpec = NPath.ProgramFilesDirectory.Combine("Unity/Hub/Editor/*/Editor");
    public static readonly string HubInstalledToolchainPathSpec = HubInstalledToolchainNPathSpec;

    internal static readonly NPath ManuallyInstalledToolchainsNPathSpec = NPath.ProgramFilesDirectory.Combine("Unity*/Editor");
    public static readonly string ManuallyInstalledToolchainsPathSpec = ManuallyInstalledToolchainsNPathSpec;

    internal static readonly NPath UnityEditorDefaultLogNPath = NPath.LocalAppDataDirectory.Combine("Unity/Editor/Editor.log");
    public static readonly string UnityEditorDefaultLogPath = UnityEditorDefaultLogNPath;

    internal static readonly NPath UpmLogNPath = NPath.LocalAppDataDirectory.Combine("Unity/Editor/upm.log");
    public static readonly string UpmLogPath = UpmLogNPath;
}

/// <summary>
/// The entry point to Unity-related queries.
/// </summary>
[PublicAPI]
public static class Unity
{
    // TODO: consider if we should auto-add common subpaths like 'Editor' or for build like 'build/*Editor*/*/*'
    // but probably have a pathspec prefix option to disable this automation, if it's not opt-in..
    static IEnumerable<UnityToolchain> FindToolchains(IEnumerable<NPath> pathSpecs, UnityToolchainOrigin? origin, bool throwOnInvalidPathSpec) => pathSpecs
        .SelectMany(p => Globbing.Find(p, UnityConstants.UnityExeName, throwOnInvalidPathSpec))
        .Select(p => UnityToolchain.TryCreateFromPath(p.Parent, origin)) // drop filename for TryCreateFromPath, which will add it back (see Globbing.Find for why we must do this hack)
        .WhereNotNull();

    // this just walks the default install dir, but the user may have customized where the hub goes, which affects
    // its default install path for unity installations it manages.
    //
    // TODO: include user-customized Hub install folder if set
    //   * %APPDATA%\UnityHub\secondaryInstallPath.json contains a quoted string
    //   * this is empty if the user hasn't customized "installs location" in hub prefs
    //   * need to test and detect the sub-spec to use..also Editor/*/Editor..?
    //
    // TODO: include Hub-known custom installs
    //   * %APPDATA%\Roaming\UnityHub\editors.json contains all installs manually added to Hub
    public static IEnumerable<UnityToolchain> FindHubInstalledToolchains() =>
        FindToolchains(UnityConstants.HubInstalledToolchainNPathSpec.WrapInEnumerable(), UnityToolchainOrigin.UnityHub, false);

    // this just walks the default install dir
    // TODO: go walk the installer stuff in the registry and discover all manually installations that way.
    public static IEnumerable<UnityToolchain> FindManuallyInstalledToolchains() =>
        FindToolchains(UnityConstants.ManuallyInstalledToolchainsNPathSpec.WrapInEnumerable(), UnityToolchainOrigin.ManuallyInstalled, false);

    public static IEnumerable<UnityToolchain> FindCustomToolchains(IEnumerable<string> pathSpecs, bool throwOnInvalidPathSpec) =>
        FindToolchains(pathSpecs.ToNPath(), null, throwOnInvalidPathSpec);

    #if NOTYET
    public static bool TryParseUnityHubUrl(string unityHubUrl)
    {
        // example: unityhub://2020.3.25f1-foo/123456789ab << last part is the hash, first part is the build

        // what the hub does..
        // 1. download "https://download.unity3d.com/download_unity/123456789ab/Windows64EditorInstaller/UnitySetup64-2020.3.25f1-foo.exe"
        //    to "C:\Users\scott\AppData\Local\Temp\unityhub-62a10bf0-9324-11ec-8e36-fd2c0e80f6f8\UnitySetup64-2020.3.25f1-foo.exe"
        // 1a. additional modules have custom installers like "UnitySetup-WebGL-Support-for-Editor-2020.3.25f1-foo.exe" or a zip like "UnityDocumentation.zip"
        //     and they will go to peer guid-named folders
        // 2. run that exe with "/S /D=C:\Program Files\Unity\Hub\Editor\2020.3.25f1-foo"
        //    ^ this causes problems when we have different hashes but otherwise same versions
        // 3. installer runs (it will do an unnecessary UAC popup if path is to a user-controlled folder) and hub waits for it to complete
    }
    #endif
}
