class SplitAtElement
{
    [TestCase("a/b/c", new[] { -1, 3 })]
    [TestCase("", new[] { -1, 0, 1 })]
    [TestCase(@"c:\path\to\some\file.txt", new[] { -1, 4 })]
    public void OutOfRange_Throws(string path, int[] badIndices)
    {
        foreach (var badIndex in badIndices)
            Should.Throw<IndexOutOfRangeException>(() => path.ToNPath().SplitAtElement(badIndex));
    }

    [TestCase("q:/path/to/thing.txt", "q")]
    [TestCase(@"q:\path\to\thing.txt", "q")]
    [TestCase("/path/to/thing.txt", null)]
    [TestCase(@"\path\to\thing.txt", null)]
    public void Absolute(string path, string driveLetter)
    {
        var npath = path.ToNPath();

        var (b, s) = npath.SplitAtElement(0);
        b.IsRelative.ShouldBeFalse();
        b.DriveLetter.ShouldBe(driveLetter);
        b.Elements.ShouldBeEmpty();
        s.IsRelative.ShouldBeTrue();
        s.Elements.ShouldBe(new[] { "path", "to", "thing.txt" });
        b.Combine(s).ToString().ShouldBe(npath.ToString());

        (b, s) = npath.SplitAtElement(1);
        b.IsRelative.ShouldBeFalse();
        b.DriveLetter.ShouldBe(driveLetter);
        b.Elements.ShouldBe(new[] { "path" });
        s.IsRelative.ShouldBeTrue();
        s.Elements.ShouldBe(new[] { "to", "thing.txt" });
        b.Combine(s).ToString().ShouldBe(npath.ToString());

        (b, s) = npath.SplitAtElement(2);
        b.IsRelative.ShouldBeFalse();
        b.DriveLetter.ShouldBe(driveLetter);
        b.Elements.ShouldBe(new[] { "path", "to" });
        s.IsRelative.ShouldBeTrue();
        s.Elements.ShouldBe(new[] { "thing.txt" });
        b.Combine(s).ToString().ShouldBe(npath.ToString());
    }

    [TestCase("path/to/thing.txt")]
    [TestCase(@"path\to\thing.txt")]
    public void Relative(string path)
    {
        var npath = path.ToNPath();

        var (b, s) = npath.SplitAtElement(0);
        b.IsRelative.ShouldBeTrue();
        b.Elements.ShouldBeEmpty();
        s.IsRelative.ShouldBeTrue();
        s.Elements.ShouldBe(new[] { "path", "to", "thing.txt" });
        b.Combine(s).ToString().ShouldBe(npath.ToString());

        (b, s) = npath.SplitAtElement(1);
        b.IsRelative.ShouldBeTrue();
        b.Elements.ShouldBe(new[] { "path" });
        s.IsRelative.ShouldBeTrue();
        s.Elements.ShouldBe(new[] { "to", "thing.txt" });
        b.Combine(s).ToString().ShouldBe(npath.ToString());

        (b, s) = npath.SplitAtElement(2);
        b.IsRelative.ShouldBeTrue();
        b.Elements.ShouldBe(new[] { "path", "to" });
        s.IsRelative.ShouldBeTrue();
        s.Elements.ShouldBe(new[] { "thing.txt" });
        b.Combine(s).ToString().ShouldBe(npath.ToString());
    }
}
