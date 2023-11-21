using System;
using NUnit.Framework;
using Shouldly;

class NiceIOTests
{
    [Test]
    public void Elements()
    {
        CollectionAssert.AreEqual(new[] {"my", "path", "to", "somewhere.txt"}, new NPath("/my/path/to/somewhere.txt").Elements);
    }

    [TestCase("a/b/c", new[] { -1, 3 })]
    [TestCase("", new[] { -1, 0, 1 })]
    [TestCase(@"c:\path\to\some\file.txt", new[] { -1, 4 })]
    public void SplitAtElement_WithOutOfRange_Throws(string path, int[] badIndices)
    {
        foreach (var badIndex in badIndices)
            Should.Throw<ArgumentOutOfRangeException>(() => path.ToNPath().SplitAtElement(badIndex));
    }

    // TODO FIXME [TestCase("q:/path/to/thing.txt", "q")]
    // TODO FIXME [TestCase(@"q:\path\to\thing.txt", "q")]
    [TestCase("/path/to/thing.txt", null)]
    [TestCase(@"\path\to\thing.txt", null)]
    public void SplitAtElement_WithAbsolute(string path, string driveLetter)
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
    public void SplitAtElement_WithRelative(string path)
    {
        var npath = path.ToNPath();

        var (b, s) = npath.SplitAtElement(0);
        b.IsRelative.ShouldBeTrue();
        b.Elements.ShouldBe(new[] { "." }); // TODO: unsure about this
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

    [TestCaseSource(nameof(TildeExpandCollapse_Source))]
    public void TildeExpandCollapse((string, string) ab)
    {
        var (a, b) = ab;
        var (an, bn) = (a.ToNPath(), b.ToNPath());

        an.TildeExpand().ShouldBe(bn);
        an.TildeExpand().TildeCollapse().ShouldBe(an);

        bn.TildeCollapse().ShouldBe(an);
        bn.TildeCollapse().TildeExpand().ShouldBe(bn);
    }

    static (string, string)[] TildeExpandCollapse_Source() => new[]
    {
        // basics

        ("~", NPath.HomeDirectory.ToString()),
        ("~/some/other/file.txt", NPath.HomeDirectory.Combine("some", "other", "file.txt").ToString()),
        ("~/some/other/file.txt", NPath.HomeDirectory.Combine("some", "other", "file.txt").ToString()),

        // unsupported tilde style

        ("~x/file.txt", "~x/file.txt"),
        ("x~/file.txt", "x~/file.txt"),

        // no tilde

        ("x/file.txt", "x/file.txt"),

        // absolutes

        ("c:/blah/file.txt", "c:/blah/file.txt"),
        ("c:/~/blah/file.txt", "c:/~/blah/file.txt"),
    };

    [Test]
    public void ChangeFilename_Absolute()
    {
        NPath expected, actual;

        expected = new NPath("/my/other.file");
        actual = new NPath("/my/file.txt").ChangeFilename("other.file");
        Assert.AreEqual(expected, actual);

        expected = new NPath("/my/folder");
        actual = new NPath("/my/path").ChangeFilename("folder");
        Assert.AreEqual(expected, actual);
    }

    [Test]
    public void ChangeFilename_Relative()
    {
        NPath expected, actual;

        expected = new NPath("my/other.file");
        actual = new NPath("my/file.txt").ChangeFilename("other.file");
        Assert.AreEqual(expected, actual);

        expected = new NPath("my/folder");
        actual = new NPath("my/path").ChangeFilename("folder");
        Assert.AreEqual(expected, actual);
    }

    [Test]
    public void ChangeFilename_ToEmptyString()
    {
        NPath expected, actual;

        expected = new NPath("/my/path");
        actual = new NPath("/my/path/file.txt").ChangeFilename("");
        Assert.AreEqual(expected, actual);

        expected = new NPath("my/path");
        actual = new NPath("my/path/file.txt").ChangeFilename("");
        Assert.AreEqual(expected, actual);
    }

    [Test]
    public void ChangeFilename_CannotChangeTheExtensionOfAWindowsRootDirectory()
    {
        Assert.Throws<ArgumentException>(() => new NPath("C:\\").ChangeFilename("file.txt"));
    }

    [Test]
    public void ChangeFilename_CannotChangeTheExtensionOfALinuxRootDirectory()
    {
        Assert.Throws<ArgumentException>(() => new NPath("/").ChangeFilename("file.txt"));
    }

    [Test]
    public void ExtensionWithDot_Simple()
    {
        Assert.AreEqual(".txt", new NPath("file.txt").ExtensionWithDot);
    }

    [Test]
    public void ExtensionWithDot_FileWithoutExtension()
    {
        Assert.AreEqual("", new NPath("myfile").ExtensionWithDot);
    }

    [Test]
    public void ExtensionWithDot_FileWithMultipleDots()
    {
        Assert.AreEqual(".exe", new NPath("myfile.something.something.exe").ExtensionWithDot);
    }

    [Test]
    public void ExtensionWithDot_ExtensionWithDotOnLinuxRoot()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            var _ = new NPath("/").ExtensionWithDot;
        });
    }

    [Test]
    public void ExtensionWithDot_ExtensionWithDotOnWindowsRoot()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            var _ = new NPath("C:\\").ExtensionWithDot;
        });
    }
}
