using System.Reflection;

[TestFixture]
abstract class TestFixtureBase
{
    protected NPath TestFiles { get; } = Assembly
        .GetExecutingAssembly()
        .GetCustomAttribute<TestFilesLocationAttribute>()!
        .Location
        .ToNPath()
        .DirectoryMustExist();
}
