class UnityProjectTests : TestFixtureBase
{
    NPath Project(string repo, string project) => TestFiles.Combine("EditorYml", repo, project).DirectoryMustExist();

    [Test]
    public void GetTestableUnityVersions_WithSameProjectVersion_ReturnsProjectVersion()
    {
        var project = UnityProject.TryCreateFromProjectRoot(Project("RepoWithEditorYml", "ProjectWithSame"));
        project.ShouldNotBeNull();

        var versions = project.GetTestableVersions().ToArray();
        versions.ShouldBe(new[]
        {
            new UnityVersion(2020, 3, 25, 'f', 1, "dots", "7017b5c35b85"),
        });
    }

    [Test]
    public void GetTestableUnityVersions_WithUniqueProjectVersion_ReturnsBothVersions()
    {
        var project = UnityProject.TryCreateFromProjectRoot(Project("RepoWithEditorYml", "ProjectWithUnique"));
        project.ShouldNotBeNull();

        var versions = project.GetTestableVersions().ToArray();
        versions.ShouldBe(new[]
        {
            new UnityVersion(2020, 3, 14, 'f', 1, "dots", "86b16565e3c0"),
            new UnityVersion(2020, 3, 25, 'f', 1, "dots", "7017b5c35b85"),

        });
    }

    [Test]
    public void GetTestableUnityVersions_WithNoEditorYml_ReturnsProjectVersion()
    {
        var project = UnityProject.TryCreateFromProjectRoot(Project("RepoWithNoEditorYml", "Project"));
        project.ShouldNotBeNull();

        var versions = project.GetTestableVersions().ToArray();
        versions.ShouldBe(new[]
        {
            new UnityVersion(2020, 3, 14, 'f', 1, "dots", "86b16565e3c0")
        });
    }
}
