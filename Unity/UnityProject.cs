using System.Dynamic;

namespace OkTools.Unity;

[PublicAPI]
public class UnityProject : IStructuredOutput
{
    readonly NPath _projectRoot;

    public string Path => _projectRoot;
    internal NPath NPath => _projectRoot;

    UnityProject(NPath projectRoot)
    {
        _projectRoot = projectRoot;
    }

    public override string ToString() => $"{NPath}: {GetProjectUnityVersion()}";

    public dynamic Output(StructuredOutputDetail detail)
    {
        var output = Expando.From(new
        {
            Path,
            ProjectUnityVersion = GetProjectUnityVersion().ToString()
        });

        if (detail >= StructuredOutputDetail.Typical)
            output.TestableUnityVersions = GetTestableUnityVersions().SelectToStrings().ToArray();

        if (detail >= StructuredOutputDetail.Full)
        {
            output.ProjectUnityVersionFull = GetProjectUnityVersion();
            output.TestableUnityVersions = GetTestableUnityVersions().ToArray();
        }

        return output;
    }

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
