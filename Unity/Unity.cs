namespace OkTools.Unity;

[PublicAPI]
public static class UnityConstants
{
    public const string UnityExeName = "unity.exe";
    public const string ProjectAssetsFolderName = "Assets";
    public const string ProjectVersionTxtFileName = "ProjectVersion.txt";
    public const string EditorsYmlFileName = "editors.yml";

    public static NPath ProjectVersionRelativePath => ProjectVersionRelativeNPath;
    internal static readonly NPath ProjectVersionRelativeNPath = new NPath("ProjectSettings").Combine(ProjectVersionTxtFileName);

    public static string MonoDllRelativePath => MonoDllRelativeNPath;
    public static string HubInstalledToolchainPathSpec => HubInstalledToolchainNPathSpec;
    public static string ManuallyInstalledToolchainsPathSpec => ManuallyInstalledToolchainsNPathSpec;

    internal static readonly NPath MonoDllRelativeNPath = "Data/MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll".ToNPath();
    internal static readonly NPath HubInstalledToolchainNPathSpec = NPath.ProgramFilesDirectory.Combine("Unity/Hub/Editor/*/Editor");
    internal static readonly NPath ManuallyInstalledToolchainsNPathSpec = NPath.ProgramFilesDirectory.Combine("Unity*/Editor");
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
}
