using System.Diagnostics;

namespace OkTools.TestUtils;

[PublicAPI]
public struct DirectoryBackup : IDisposable
{
    public DirectoryBackup(string folderPath)
    {
        _backupPath = Path.GetTempPath().ToNPath().Combine(Process.GetCurrentProcess().Id.ToString());
        _fullPath = folderPath.ToNPath().MakeAbsolute();

        Directory.CreateDirectory(_backupPath);
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

[PublicAPI]
public abstract class TestFileSystemFixture
{
    protected NPath BaseDir { private set; get; } = null!;
    protected string Eol { set; get; } = "\n";
    protected string TestDirectory { set; get; } = TestContext.CurrentContext.TestDirectory;

    [OneTimeSetUp]
    public void InitFixture()
    {
        // TODO: put in uniquely-named subdir so can parallelize across fixtures

        BaseDir = TestDirectory.ToNPath().Combine("testfs");
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
    }

    protected NPath WriteAllLines(NPath path, params string[] lines) =>
        WriteAllLines(path, lines.AsEnumerable());

    protected NPath WriteAllLines(NPath path, IEnumerable<string> lines) =>
        path.WriteAllText(lines.Append("").StringJoin(Eol));
}
