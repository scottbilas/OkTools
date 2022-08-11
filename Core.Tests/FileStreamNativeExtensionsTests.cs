#pragma warning disable CA1001
class FileStreamNativeExtensionsTests : TestFileSystemFixture
#pragma warning restore CA1001
{
    NPath _path = null!;
    FileStream _stream = null!;

    [SetUp]
    public void SetUp()
    {
        _path = BaseDir.Combine("test.dat");
        _path.DeleteIfExists();
        _path.WriteAllText(TestContext.CurrentContext.Test.Name);
        _stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    }

    [TearDown]
    public void TearDown()
    {
        _path.DeleteIfExists();
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (_stream != null)
        {
            _stream.Dispose();
            _stream = null!;
        }
    }

    [Test]
    public void WasFileDeleted_WithExistingFile_ReturnsFalse()
    {
        _stream.WasFileDeleted().ShouldBeFalse();
    }

    [Test]
    public void WasFileDeleted_WithDeletedFile_ReturnsTrue()
    {
        _path.Delete();
        _stream.WasFileDeleted().ShouldBeTrue();
    }

    [Test]
    public void WasFileDeleted_WithOverwrittenFile_ReturnsFalse() // overwriting a file does not delete it first
    {
        _path.WriteAllText("Overwritten!");
        _stream.WasFileDeleted().ShouldBeFalse();
    }

    [Test]
    public void WasFileDeleted_WithDeletedAndRecreatedFile_ReturnsTrue()
    {
        _path.Delete();
        _path.WriteAllText("Recreated!");
        _stream.WasFileDeleted().ShouldBeTrue();
    }

    [Test]
    public void WasFileDeleted_WithClosedFile_Throws()
    {
        _stream.Close();
        Should.Throw<ObjectDisposedException>(() => _stream.WasFileDeleted());
    }
}
