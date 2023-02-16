namespace OkTools.Unity;

[PublicAPI]
public class UnityProject : IStructuredOutput
{
    readonly NPath _projectRoot;

    public string Name => _projectRoot.FileName;
    public string Path => _projectRoot;
    internal NPath NPath => _projectRoot;

    UnityProject(NPath projectRoot)
    {
        _projectRoot = projectRoot.MakeAbsolute();
    }

    public override string ToString() => $"{NPath}: {GetVersion()}";

    public object Output(StructuredOutputLevel level, bool debug)
    {
        var output = Expando.From(new
        {
            Path,
            Version = GetVersion().ToString(),
            Created = GetCreationTime().ToNiceAge(true),
            LastOpened = GetLastOpenedTime()?.ToNiceAge(true) ?? "never",
        });

        if (level >= StructuredOutputLevel.Normal)
            output.TestableUnityVersions = GetTestableVersions().SelectToStrings().ToArray();

        if (level >= StructuredOutputLevel.Detailed)
        {
            output.VersionFull = GetVersion();
            output.TestableVersions = GetTestableVersions().ToArray();

            //$$$LibGit2Sharp.
        }

        // TODO: some kind of last-write timestamp
        // TODO: git info (root, branch at least, anything that's instant and doesn't require file walking)

        return output;
    }

    // TODO: project last opened, project last modified (SourceAssetDB, ArtifactDB..?)

    public UnityVersion GetVersion()
    {
        var projectVersionNPath = _projectRoot.Combine(UnityProjectConstants.ProjectVersionTxtNPath);
        return UnityVersion.FromUnityProjectVersionTxt(projectVersionNPath);
    }

    public IEnumerable<UnityVersion> GetTestableVersions()
    {
        var projectVersion = GetVersion();
        yield return projectVersion;

        var editorsYml = _projectRoot.ParentContaining(UnityConstants.EditorsYmlFileName, true);
        if (editorsYml is null)
            yield break;

        foreach (var version in UnityVersion.FromEditorsYml(editorsYml))
        {
            if (version != projectVersion)
                yield return version;
        }
    }

    // returns null if never opened
    public DateTime? GetLastOpenedTime()
    {
        var artifactDb = _projectRoot.Combine(UnityProjectConstants.ArtifactDbNPath);
        if (!artifactDb.FileExists())
            return null;

        return artifactDb.FileInfo.LastWriteTime;
    }

    public DateTime GetCreationTime()
    {
        return _projectRoot.Combine(UnityProjectConstants.AssetsNPath).FileInfo.CreationTime;
    }

    // only tests this folder as root
    public static UnityProject? TryCreateFromProjectRoot(string pathToUnityProject) =>
        TryCreateFromProjectRoot(pathToUnityProject.ToNPath());
    internal static UnityProject? TryCreateFromProjectRoot(NPath pathToUnityProject)
    {
        // TODO: tests for this
        // ALSO TODO: switch from files on disk to tempfs (nuget package to support this..?)

        // must have an assets folder (unity rule)
        if (!pathToUnityProject.Combine(UnityProjectConstants.AssetsNPath).DirectoryExists())
            return null;

        // must have a projectversion file
        if (!pathToUnityProject.Combine(UnityProjectConstants.ProjectVersionTxtNPath).FileExists())
        {
            // test projects may not commit the projectversion, so try this one
            if (!pathToUnityProject.Combine(UnityProjectConstants.ManifestJsonNPath).FileExists())
                return null;
        }

        return new UnityProject(pathToUnityProject);
    }

    // walks up ancestry to test for root
    public static UnityProject? TryCreateFromProjectTree(string pathToTest) =>
        TryCreateFromProjectTree(pathToTest.ToNPath());
    internal static UnityProject? TryCreateFromProjectTree(NPath pathToTest)
    {
        pathToTest = pathToTest.MakeAbsolute();

        // probably a mistake if we were given an explicit file, rather than some directory (likely CWD) under project root
        if (pathToTest.FileExists())
            return null;

        for (;;)
        {
            var project = TryCreateFromProjectRoot(pathToTest);
            if (project != null)
                return project;

            if (pathToTest.IsRoot)
                return null;

            pathToTest = pathToTest.Parent;
        }
    }
}
