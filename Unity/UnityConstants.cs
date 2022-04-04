namespace OkTools.Unity;

[PublicAPI]
public static class UnityConstants
{
    // TODO: platform!

    public const string UnityProcessName = "Unity";
    public const string UnityExeName = "Unity.exe";
    public const string EditorsYmlFileName = "editors.yml";

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

[PublicAPI]
public static class UnityProjectConstants
{
    public const string SceneFileExtension = ".unity";

    public const string AssetsFolderName = "Assets";
    internal static readonly NPath AssetsNPath = new(AssetsFolderName);
    public static readonly string AssetsPath = AssetsNPath;

    public const string LibraryFolderName = "Library";
    internal static readonly NPath LibraryNPath = new(LibraryFolderName);
    public static readonly string LibraryPath = LibraryNPath;

        public const string ArtifactDbFileName = "ArtifactDB";
        internal static readonly NPath ArtifactDbNPath = LibraryNPath.Combine(ArtifactDbFileName);
        public static readonly string ArtifactDbPath = ArtifactDbNPath;

        public const string LastSceneManagerSetupFileName = "LastSceneManagerSetup.txt";
        internal static readonly NPath LastSceneManagerSetupNPath = LibraryNPath.Combine(LastSceneManagerSetupFileName);
        public static readonly string LastSceneManagerSetupPath = LastSceneManagerSetupNPath;

    public const string ProjectSettingsFolderName = "ProjectSettings";
    internal static readonly NPath ProjectSettingsNPath = new(ProjectSettingsFolderName);
    public static readonly string ProjectSettingsPath = ProjectSettingsNPath;

        public const string ProjectVersionTxtFileName = "ProjectVersion.txt";
        internal static readonly NPath ProjectVersionTxtNPath = ProjectSettingsNPath.Combine(ProjectVersionTxtFileName);
        public static readonly string ProjectVersionTxtPath = ProjectVersionTxtNPath;

    public const string LogsFolderName = "Logs";
    internal static readonly NPath LogsNPath = new(LogsFolderName);
    public static readonly string LogsPath = LogsNPath;
}
