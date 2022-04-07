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

        PatchUtility.IsDiff(diffText).ShouldBeTrue();
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

        PatchUtility.IsDiff(diffText).ShouldBeTrue();
    }

    [Test]
    public void IsDiff_EmptyDiff_ReturnsFalse()
    {
        PatchUtility.IsDiff("").ShouldBeFalse();
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

        PatchUtility.IsDiff(diffText).ShouldBeFalse();
    }

    [Test]
    public void IsDiff_IncompleteDiff_ReturnsFalse()
    {
        var diffText = new[]
        {
            "--- a/folder/another/some_file.cs",
            "+++ b/folder/another/some_file.cs",
        }.StringJoin('\n');

        PatchUtility.IsDiff(diffText).ShouldBeFalse();
    }
}
