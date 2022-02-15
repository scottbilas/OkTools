class DiffUtilsTests
{
    [Test]
    public void IsDiff_ValidLfDiff_ReturnsTrue()
    {
        var diffText = new[]
        {
            "--- a/folder/another/some_file.cs",
            "+++ b/folder/another/some_file.cs",
            "@@ -1,6 +1,7 @@",
        }.StringJoin('\n');

        PatchUtils.IsDiff(diffText).ShouldBeTrue();
    }

    [Test]
    public void IsDiff_ValidCrLfDiff_ReturnsTrue()
    {
        var diffText = new[]
        {
            "--- a/folder/another/some_file.cs",
            "+++ b/folder/another/some_file.cs",
            "@@ -1,6 +1,7 @@",
        }.StringJoin("\r\n");

        PatchUtils.IsDiff(diffText).ShouldBeTrue();
    }

    [Test]
    public void IsDiff_EmptyDiff_ReturnsFalse()
    {
        PatchUtils.IsDiff("").ShouldBeFalse();
    }

    [Test]
    public void IsDiff_BrokenDiff_ReturnsFalse()
    {
        var diffText = new[]
        {
            "--- a/folder/another/some_file.cs",
            " +++ b/folder/another/some_file.cs",
            "@@ -1,6 +1,7 @@"
        }.StringJoin('\n');

        PatchUtils.IsDiff(diffText).ShouldBeFalse();
    }

    [Test]
    public void IsDiff_IncompleteDiff_ReturnsFalse()
    {
        var diffText = new[]
        {
            "--- a/folder/another/some_file.cs",
            "+++ b/folder/another/some_file.cs",
        }.StringJoin('\n');

        PatchUtils.IsDiff(diffText).ShouldBeFalse();
    }
}
