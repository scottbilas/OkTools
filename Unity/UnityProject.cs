﻿namespace OkTools.Unity;

[PublicAPI]
public class UnityProject : IStructuredOutput
{
    readonly NPath _projectRoot;

    public string Path => _projectRoot;
    internal NPath NPath => _projectRoot;

    UnityProject(NPath projectRoot)
    {
        _projectRoot = projectRoot.MakeAbsolute();
    }

    public override string ToString() => $"{NPath}: {GetProjectUnityVersion()}";

    public object Output(StructuredOutputLevel level, bool debug)
    {
        var output = Expando.From(new
        {
            Path,
            ProjectUnityVersion = GetProjectUnityVersion().ToString(),
            Created = GetCreationTime().ToNiceAge(true),
            LastOpened = GetLastOpenedTime()?.ToNiceAge(true) ?? "never",
        });

        if (level >= StructuredOutputLevel.Normal)
            output.TestableUnityVersions = GetTestableUnityVersions().SelectToStrings().ToArray();

        if (level >= StructuredOutputLevel.Detailed)
        {
            output.ProjectUnityVersionFull = GetProjectUnityVersion();
            output.TestableUnityVersions = GetTestableUnityVersions().ToArray();

            //$$$LibGit2Sharp.
        }

        // TODO: some kind of last-write timestamp
        // TODO: git info (root, branch at least, anything that's instant and doesn't require file walking)

        return output;
    }

    // TODO: project last opened, project last modified (SourceAssetDB, ArtifactDB..?)

    public UnityVersion GetProjectUnityVersion()
    {
        var projectVersionNPath = _projectRoot.Combine(UnityConstants.ProjectVersionRelativeNPath);
        return UnityVersion.FromUnityProjectVersionTxt(projectVersionNPath);
    }

    public IEnumerable<UnityVersion> GetTestableUnityVersions()
    {
        var projectVersion = GetProjectUnityVersion();
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
        var artifactDb = _projectRoot.Combine(UnityConstants.ArtifactDbRelativeNPath);
        if (!artifactDb.FileExists())
            return null;

        return artifactDb.FileInfo.LastWriteTime;
    }

    public DateTime GetCreationTime()
    {
        return _projectRoot.Combine(UnityConstants.ProjectAssetsFolderName).FileInfo.CreationTime;
    }

    public static UnityProject? TryCreateFromProjectRoot(string pathToUnityProject) =>
        TryCreateFromProjectRoot(pathToUnityProject.ToNPath());
    internal static UnityProject? TryCreateFromProjectRoot(NPath pathToUnityProject)
    {
        // must have an assets folder (unity rule)
        if (!pathToUnityProject.Combine(UnityConstants.ProjectAssetsFolderName).DirectoryExists())
            return null;

        // must have a projectversion file
        if (!pathToUnityProject.Combine(UnityConstants.ProjectVersionRelativeNPath).FileExists())
            return null;

        return new UnityProject(pathToUnityProject);
    }
}
