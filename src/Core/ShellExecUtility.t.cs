class ShellExecUtilityTests : TempFileSystemFixture
{
    [Test]
    public void TryResolveExecutableExtension_ReturnsMatchesInPriorityOrder()
    {
        NPath.SetCurrentDirectory(BaseDir.Combine("priority-order").CreateDirectory());

        var ps1 = "test.ps1".ToNPath().CreateFile();
        var com = "test.com".ToNPath().CreateFile();
        var exe = "test.exe".ToNPath().CreateFile();
        var bat = "test.bat".ToNPath().CreateFile();
        var cmd = "test.cmd".ToNPath().CreateFile();

        // check that a direct match always works
        ShellExecUtility.TryResolveExecutableExtension("test.ps1").ShouldBe(ps1);
        ShellExecUtility.TryResolveExecutableExtension("test.com").ShouldBe(com);
        ShellExecUtility.TryResolveExecutableExtension("test.exe").ShouldBe(exe);
        ShellExecUtility.TryResolveExecutableExtension("test.bat").ShouldBe(bat);
        ShellExecUtility.TryResolveExecutableExtension("test.cmd").ShouldBe(cmd);

        // invalid filename shouldn't find anything
        ShellExecUtility.TryResolveExecutableExtension("test2").ShouldBeNull();
        ShellExecUtility.TryResolveExecutableExtension("test.foo").ShouldBeNull();

        // now test that lookup order is preserved if extension not specified
        ShellExecUtility.TryResolveExecutableExtension("test").ShouldBe(ps1);
        ps1.Delete();
        ShellExecUtility.TryResolveExecutableExtension("test").ShouldBe(com);
        com.Delete();
        ShellExecUtility.TryResolveExecutableExtension("test").ShouldBe(exe);
        exe.Delete();
        ShellExecUtility.TryResolveExecutableExtension("test").ShouldBe(bat);
        bat.Delete();
        ShellExecUtility.TryResolveExecutableExtension("test").ShouldBe(cmd);
        cmd.Delete();

        // shouldn't find anything after files deleted
        ShellExecUtility.TryResolveExecutableExtension("test").ShouldBeNull();
        ShellExecUtility.TryResolveExecutableExtension("test.cmd").ShouldBeNull();
    }

    [Test]
    public void TryResolveExecutablePath_WithRelativePath()
    {
        NPath.SetCurrentDirectory(BaseDir.Combine("relative-path").CreateDirectory());

        var localBat = "local.bat".ToNPath().CreateFile();

        var subPath = "sub".ToNPath().CreateDirectory();
        var subBat = subPath.Combine("sub.bat").CreateFile();

        ShellExecUtility.TryResolveExecutablePath("local.bat", "").ShouldBe(localBat);
        ShellExecUtility.TryResolveExecutablePath("local.bat", "sub").ShouldBe(localBat);
        ShellExecUtility.TryResolveExecutablePath("./local.bat", "").ShouldBe(localBat);
        ShellExecUtility.TryResolveExecutablePath("./local.bat", "sub").ShouldBe(localBat);

        ShellExecUtility.TryResolveExecutablePath("sub.bat", "").ShouldBeNull();
        ShellExecUtility.TryResolveExecutablePath("sub.bat", "sub").ShouldBe(subBat);
        ShellExecUtility.TryResolveExecutablePath("./sub.bat", "").ShouldBeNull();
        ShellExecUtility.TryResolveExecutablePath("./sub.bat", "sub").ShouldBeNull();
        ShellExecUtility.TryResolveExecutablePath("sub/sub.bat", "").ShouldBe(subBat);
        ShellExecUtility.TryResolveExecutablePath("sub/sub.bat", "sub").ShouldBe(subBat);
        ShellExecUtility.TryResolveExecutablePath("./sub/sub.bat", "").ShouldBe(subBat);
        ShellExecUtility.TryResolveExecutablePath("./sub/sub.bat", "sub").ShouldBe(subBat);
    }

    // TODO: TESTS TO WRITE
    //
    // check that "./jam foo" does not search outside current directory (no PATH)
}
