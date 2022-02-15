class SafeFileTests : TestFileSystemFixture
{
    [Test]
    public void SetReadOnly_AppliesProperFileAttributes()
    {
        var path = BaseDir.CreateFile("normal.txt");
        ((File.GetAttributes(path) & FileAttributes.ReadOnly) == 0).ShouldBeTrue();

        SafeFile.SetReadOnly(path);
        ((File.GetAttributes(path) & FileAttributes.ReadOnly) != 0).ShouldBeTrue();
        SafeFile.SetReadOnly(path, false);
        ((File.GetAttributes(path) & FileAttributes.ReadOnly) == 0).ShouldBeTrue();
    }

    [Test]
    public void ForceDeleteFile_WithNormalFile_DeletesIt()
    {
        var path = BaseDir.CreateFile("normal.txt");

        Should.NotThrow(() => SafeFile.ForceDeleteFile(path));
        path.FileExists().ShouldBeFalse();
    }

    [Test]
    public void ForceDeleteFile_WithReadOnlyFile_DeletesIt()
    {
        var path = BaseDir.CreateFile("readonly.txt");
        SafeFile.SetReadOnly(path);

        Should.NotThrow(() => SafeFile.ForceDeleteFile(path));
        path.FileExists().ShouldBeFalse();
    }

    [Test]
    public void ForceDeleteFile_WithMissingFile_DoesNotThrow()
    {
        var path = BaseDir.Combine("missing.txt");

        path.FileExists().ShouldBeFalse();
        Should.NotThrow(() => SafeFile.ForceDeleteFile(path));
        path.FileExists().ShouldBeFalse();
    }

    [Test]
    public void AtomicWrite_WithExistingReadOnlyTempAndBakFiles_OverwritesFilesAndOperatesNormally()
    {
        var path = BaseDir.Combine("test.txt").WriteAllText("test");
        var temp = (path + SafeFile.TmpExtension).ToNPath().CreateFile();
        var backup = (path + SafeFile.BakExtension).ToNPath().CreateFile();

        SafeFile.SetReadOnly(temp);
        SafeFile.SetReadOnly(backup);

        SafeFile.AtomicWrite(path, tmpPath => tmpPath.ToNPath().WriteAllText("new"));

        temp.FileExists().ShouldBeFalse();
        backup.FileExists().ShouldBeFalse();
        path.ReadAllText().ShouldBe("new");
    }

    [Test]
    public void AtomicWrite_ReplacingExistingReadOnlyFile_Throws()
    {
        var path = BaseDir.CreateFile("test.txt");
        SafeFile.SetReadOnly(path);

        Should.Throw<UnauthorizedAccessException>(() =>
            SafeFile.AtomicWrite(path, tmpPath => tmpPath.ToNPath().CreateFile()));
    }

    [Test]
    public void AtomicWrite_WithEmptyAction_ShouldDoNothing()
    {
        var path = BaseDir.CreateFile("test.txt");
        SafeFile.AtomicWrite(path, _ => {});

        path.FileExists().ShouldBeTrue();
        (path + SafeFile.TmpExtension).ToNPath().FileExists().ShouldBeFalse();
        (path + SafeFile.BakExtension).ToNPath().FileExists().ShouldBeFalse();
    }
}
