using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Builders;

struct DirectoryBackup : IDisposable
{
    public DirectoryBackup(string folderPath)
    {
        _backupPath = Path.GetTempPath().ToNPath().Combine(Environment.ProcessId.ToString());
        _fullPath = folderPath.ToNPath().MakeAbsolute();

        Directory.CreateDirectory(_backupPath.ToString()!);
        _fullPath.CopyFiles(_backupPath, true);
    }

    public void Dispose()
    {
        _backupPath.CopyFiles(_fullPath, true);
        _backupPath.Delete();
    }

    NPath _backupPath;
    NPath _fullPath;
}

abstract class TestFileSystemFixture
{
    NPath _rootDir = null!;
    protected NPath BaseDir { private set; get; } = null!;
    protected string Eol { set; get; } = "\n";
    protected string TestDirectory { set; get; } = TestContext.CurrentContext.TestDirectory;

    [OneTimeSetUp]
    public void InitFixture()
    {
        _rootDir = TestDirectory.ToNPath().Combine("testfs");
        BaseDir = _rootDir.Combine(GetType().Name);
        DeleteTestFileSystem();
    }

    [OneTimeTearDown]
    public void TearDownFixture() => DeleteTestFileSystem();

    [SetUp]
    public void InitTest()
    {
        if (!BaseDir.Exists())
            BaseDir.CreateDirectory();
    }

    [TearDown]
    public void CleanupTest() => DeleteTestFileSystem();

    protected void DeleteTestFileSystem()
    {
        if (!BaseDir.Exists())
            return;

        // TODO: add support for handling readonly files/dirs to NiceIO

        foreach (var path in BaseDir
                 .Contents(true)
                 .Where(f => (File.GetAttributes(f) & FileAttributes.ReadOnly) != 0))
        {
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
        }

        BaseDir.Delete();
        if (_rootDir.Contents().Length == 0)
            _rootDir.Delete();
    }

    protected NPath WriteAllLines(NPath path, params string[] lines) =>
        WriteAllLines(path, lines.AsEnumerable());

    protected NPath WriteAllLines(NPath path, IEnumerable<string> lines) =>
        path.WriteAllText(lines.Append("").StringJoin(Eol));
}

// https://stackoverflow.com/a/43339950/14582
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
class TestCaseGenericAttribute : TestCaseAttribute, ITestBuilder
{
    public TestCaseGenericAttribute(params object[] arguments)
        : base(arguments)
    {
    }

    public Type[]? TypeArguments { get; set; }

    IEnumerable<TestMethod> ITestBuilder.BuildFrom(IMethodInfo method, Test? suite)
    {
        if (!method.IsGenericMethodDefinition)
            return base.BuildFrom(method, suite);

        if (TypeArguments == null || TypeArguments.Length != method.GetGenericArguments().Length)
        {
            var parms = new TestCaseParameters { RunState = RunState.NotRunnable };
            parms.Properties.Set("_SKIPREASON", $"{nameof(TypeArguments)} should have {method.GetGenericArguments().Length} elements");
            return new[] { new NUnitTestCaseBuilder().BuildTestMethod(method, suite, parms) };
        }

        var genMethod = method.MakeGenericMethod(TypeArguments);
        return base.BuildFrom(genMethod, suite);
    }
}
