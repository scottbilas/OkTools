partial class NiceIOTests
{
    [TestCase("file.txt", ".txt")]
    [TestCase("path/to/file.txt", ".txt")]
    [TestCase("myfile", "")]
    [TestCase("path/to/myfile", "")]
    [TestCase("myfile.something.something.exe", ".exe")]
    [TestCase("path/to/myfile.something.something.exe", ".exe")]
    public void ExtensionWithDot(string path, string expected)
    {
        new NPath(path).ExtensionWithDot.ShouldBe(expected);
    }
}
