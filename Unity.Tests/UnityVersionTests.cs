class UnityVersionTests : TestFixtureBase
{
    NPath ProjectVersionTxt(string name) => TestFiles.Combine("ProjectVersionTxt", name).FileMustExist();
    NPath EditorYml(string name) => TestFiles.Combine("EditorYml", name).FileMustExist();

    // TODO: comparisons and equality

    [Test]
    public void Ctor_WithEmptyString_Throws()
    {
        Should.Throw<UnityVersionFormatException>(() => new UnityVersion(""));
    }

    static void CheckParse(string versionText, UnityVersion expected)
    {
        var version = new UnityVersion(versionText);
        version.ShouldBe(expected);
        version.ToString().ShouldBe(versionText);
    }

    [Test]
    public void Ctor_WithFullVersionString_ReturnsFilledVersionClass()
    {
        CheckParse("2020.3.24f1-dots_a675c7af6899", new UnityVersion(2020, 3, 24, 'f', 1, "dots", "a675c7af6899"));
    }

    [Test]
    public void Ctor_WithOldStyleProductVersion_ReturnsFilledVersionClass()
    {
        var version = new UnityVersion("2018.1.6.5753908", UnityVersion.NormalizeLegacy.Yes);
        var expected = new UnityVersion(2018, 1, 6, hash: "57cc34");
        version.ShouldBe(expected);
        version.ToString().ShouldBe("2018.1.6_57cc34");
    }

    [Test]
    public void Ctor_WithPartialVersionString_ReturnsPartialFilledClass()
    {
        CheckParse("2020.3.25", new UnityVersion(2020, 3, 25));
        CheckParse("2020.3.25f1", new UnityVersion(2020, 3, 25, 'f', 1));
        CheckParse("2020.3.25f1-c07b56b34d4b", new UnityVersion(2020, 3, 25, 'f', 1, "c07b56b34d4b"));
        CheckParse("2020.3.25f1_c07b56b34d4b", new UnityVersion(2020, 3, 25, 'f', 1, null, "c07b56b34d4b"));
        CheckParse("2020.3.24f1-dots_a675c7af6899", new UnityVersion(2020, 3, 24, 'f', 1, "dots", "a675c7af6899"));
        CheckParse("2017.2.1", new UnityVersion(2017, 2, 1));
        CheckParse("2017", new UnityVersion(2017));
        CheckParse("2017.3", new UnityVersion(2017, 3));
    }

    [Test]
    public void Ctor_OptionalParts()
    {
        CheckParse("123", new UnityVersion(123));
        CheckParse("123_badf00d12345", new UnityVersion(123, hash:"badf00d12345"));
        CheckParse("123-xyzzy_badf00d12345", new UnityVersion(123, branch:"xyzzy", hash:"badf00d12345"));
        CheckParse("123.45", new UnityVersion(123, 45));
        CheckParse("123.45.67", new UnityVersion(123, 45, 67));
        CheckParse("123.45.67_badf00d12345", new UnityVersion(123, 45, 67, hash:"badf00d12345"));
        CheckParse("123.45.67-xyzzy_badf00d12345", new UnityVersion(123, 45, 67, branch:"xyzzy", hash:"badf00d12345"));
        CheckParse("123.45.67z", new UnityVersion(123, 45, 67, 'z'));
        CheckParse("123.45.67z89", new UnityVersion(123, 45, 67, 'z', 89));
        CheckParse("123.45.67z89-xyzzy", new UnityVersion(123, 45, 67, 'z', 89, "xyzzy"));
        CheckParse("123.45.67z89_badf00d12345", new UnityVersion(123, 45, 67, 'z', 89, null, "badf00d12345"));
        CheckParse("123.45.67z89-xyzzy_badf00d12345", new UnityVersion(123, 45, 67, 'z', 89, "xyzzy", "badf00d12345"));
    }

    [Test]
    public void Ctor_WithNullVsDefault_ReturnsDifferentVersions()
    {
        new UnityVersion("2020.3.24f1").Branch.ShouldBeNull();
        new UnityVersion("2020.3.24f0").Incremental.ShouldBe(0);
        new UnityVersion("2020.3.24f").Incremental.ShouldBeNull();
        new UnityVersion("2020.3.24").ReleaseType.ShouldBeNull();
        new UnityVersion("2020.3.0").Revision.ShouldBe(0);
        new UnityVersion("2020.3").Revision.ShouldBeNull();
        new UnityVersion("2020.0").Minor.ShouldBe(0);
        new UnityVersion("2020").Minor.ShouldBeNull();
    }

    [Test]
    public void Ctor_WithFullLengthHash_ReturnsTruncatedHash()
    {
        var version = new UnityVersion("2020.3.24f1-dots_a675c7af6899afe3fa4ab5acd");
        version.ShouldBe(new UnityVersion(2020, 3, 24, 'f', 1, "dots", "a675c7af6899"));
        version.ToString().ShouldBe("2020.3.24f1-dots_a675c7af6899");
    }

    [Test]
    public void Ctor_WithIllegalHash_Throws()
    {
        Should.Throw<UnityVersionFormatException>(() => new UnityVersion("2020.3.24f1-dots_a675c7af6899xoxoafe3f"));
        Should.Throw<UnityVersionFormatException>(() => new UnityVersion("2020.3.24f1-dots_"));
        Should.Throw<UnityVersionFormatException>(() => new UnityVersion("2020.3.24f1_"));
    }

    [Test]
    public void Ctor_WithIllegalBranch_Throws()
    {
        Should.Throw<UnityVersionFormatException>(() => new UnityVersion("2020.3.24f1-"));
    }

    [Test]
    public void Ctor_WithIllegalReleaseType_Throws()
    {
        Should.Throw<UnityVersionFormatException>(() => new UnityVersion("2020.3.24\0"));
        Should.Throw<UnityVersionFormatException>(() => new UnityVersion("2020.3.24#"));
        Should.Throw<UnityVersionFormatException>(() => new UnityVersion("2020.3.24ff"));
    }

    [Test]
    public void IsMatch_Scenarios()
    {
        new UnityVersion(2020).IsMatch(new UnityVersion(2020, 1)).ShouldBeTrue();
        new UnityVersion(2020, 1).IsMatch(new UnityVersion(2020)).ShouldBeTrue();
        new UnityVersion(2020, 1).IsMatch(new UnityVersion(2020, 2)).ShouldBeFalse();

        new UnityVersion(2020).IsMatch(new UnityVersion(2020, 1, 3, 'x')).ShouldBeTrue();
        new UnityVersion(2020, 1, 3).IsMatch(new UnityVersion(2020, 1, 3, 'x')).ShouldBeTrue();
        new UnityVersion(2020, 1, 2, 'x').IsMatch(new UnityVersion(2020, 1, 3, 'x')).ShouldBeFalse();

        new UnityVersion(1234, 56, 78, 'e', 910, "branch", "deadbeef").IsMatch(new UnityVersion(1234, 56, 78, 'e')).ShouldBeTrue();
        new UnityVersion(1234, 56, 78, 'e', 910, "branch", "deadbeef").IsMatch(new UnityVersion(1234)).ShouldBeTrue();
        new UnityVersion(1234, 56, 78, 'e', 910, "branch", "deadbeef").IsMatch(new UnityVersion(1234, 56, 78)).ShouldBeTrue();
        new UnityVersion(1234, 56, 78, 'e', 910, "branch", "deadbeef").IsMatch(new UnityVersion(1234, 56, 78, 'f', 910)).ShouldBeFalse();
        new UnityVersion(1234, 56, 78, 'e', 910, "branch", "deadbeef").IsMatch(new UnityVersion(1234, 56, 78, 'e', 910, "BRANCH", "DEADbeef")).ShouldBeTrue();
    }

    [Test]
    public void FromUnityProjectVersionTxt_WithValid_ReturnsParsedVersion()
    {
        var version = UnityVersion.FromUnityProjectVersionTxt(ProjectVersionTxt("Valid.txt"));
        version.ShouldBe(new UnityVersion(2020, 3, 14, 'f', 1, "dots", "86b16565e3c0"));
    }

    [Test]
    public void FromUnityProjectVersionTxt_WithValidOldStyle_ReturnsParsedVersion()
    {
        var version = UnityVersion.FromUnityProjectVersionTxt(ProjectVersionTxt("ValidOldStyle.txt"));
        version.ShouldBe(new UnityVersion(2020, 3, 14, 'f', 1));
    }

    [Test]
    public void FromUnityProjectVersionTxt_WithInvalid_Throws()
    {
        Should.Throw<UnityVersionFormatException>(() => UnityVersion.FromUnityProjectVersionTxt(ProjectVersionTxt("Invalid.txt")));
    }

    [Test]
    public void FromEditorsYml_WithMultiple_ReturnsParsedVersions()
    {
        var versions = UnityVersion.FromEditorsYml(EditorYml("Multiple.yml")).ToArray();
        versions.ShouldBe(new[]
        {
            new UnityVersion(2020, 3, 25, 'f', 1, "foo", "7017b5c35b85"),
            new UnityVersion(2021, 4, hash: "2341b5c35b85"),
            new UnityVersion(2022, 1, 0, 'b', 2, hash: "af8db9678d92"),
        });
    }

    [Test]
    public void FromEditorsYml_WithSingle_ReturnsParsedVersion()
    {
        var versions = UnityVersion.FromEditorsYml(EditorYml("Single.yml")).ToArray();
        versions.Length.ShouldBe(1);
        versions[0].ShouldBe(new UnityVersion(2020, 3, 25, 'f', 1, "dots", "7017b5c35b85"));
    }

    [Test]
    public void FromEditorsYml_WithInvalid_Throws()
    {
        Should.Throw<UnityVersionFormatException>(() => UnityVersion.FromEditorsYml(EditorYml("InvalidOuter.yml")).ToArray());
        Should.Throw<UnityVersionFormatException>(() => UnityVersion.FromEditorsYml(EditorYml("InvalidInner.yml")).ToArray());
    }
}
