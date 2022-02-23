namespace OkTools.Unity;

[PublicAPI]
public static class UnityConstants
{
    // TODO: platform!

    public const string UnityProcessName = "Unity";
    public const string UnityExeName = "Unity.exe";
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
