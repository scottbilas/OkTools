class TextUtilityTests
{
    [Test]
    public void DetectEolType_WithStopAfterWindow_ReturnsMatchAfterStopping()
    {
        TextUtility.DetectEolType("\r\n \n \n", 0).ShouldBe("\n");
        TextUtility.DetectEolType("\r\n \n \n", 1).ShouldBe("\r\n");
        TextUtility.DetectEolType("\r\n \n \n", 2).ShouldBe("\n");
        TextUtility.DetectEolType("\r\n \n \n \n \r\n \r\n", 2).ShouldBe("\n");
    }

    [Test]
    public void DetectEolType_WithNoEols_ReturnsSystemEol()
    {
        TextUtility.DetectEolType("").ShouldBe(Environment.NewLine);
        TextUtility.DetectEolType("abc").ShouldBe(Environment.NewLine);
    }

    [Test]
    public void DetectEolType_WithBasicEols_MatchesExpected()
    {
        TextUtility.DetectEolType("\n").ShouldBe("\n");
        TextUtility.DetectEolType("\n \n").ShouldBe("\n");
        TextUtility.DetectEolType("\r\n").ShouldBe("\r\n");
        TextUtility.DetectEolType("\r\n \r\n").ShouldBe("\r\n");
        TextUtility.DetectEolType("\r").ShouldBe("\n");
        TextUtility.DetectEolType("\r \r").ShouldBe("\n");
    }

    [Test]
    public void DetectEolType_WithAmbiguousEols_ReturnsSystemEol()
    {
        TextUtility.DetectEolType("\r\n \n").ShouldBe(Environment.NewLine);
        TextUtility.DetectEolType("\n \r\n").ShouldBe(Environment.NewLine);
        TextUtility.DetectEolType("\n \r\n \r\n \n").ShouldBe(Environment.NewLine);
    }

    [Test]
    public void DetectEolType_WithMismatchedEols_ReturnsMoreCommonEol()
    {
        TextUtility.DetectEolType("\r\n \r\n \n").ShouldBe("\r\n");
        TextUtility.DetectEolType("\n \r\n \n").ShouldBe("\n");
    }

    [Test]
    public unsafe void ToFourCc()
    {
        var fourcc = TextUtility.ToFourCc("WXYZ");

        ((fourcc >>  0) & 0xff).ShouldBe('W');
        ((fourcc >>  8) & 0xff).ShouldBe('X');
        ((fourcc >> 16) & 0xff).ShouldBe('Y');
        ((fourcc >> 24) & 0xff).ShouldBe('Z');

        var ptr = (byte*)&fourcc;

        ((char)ptr[0]).ShouldBe('W');
        ((char)ptr[1]).ShouldBe('X');
        ((char)ptr[2]).ShouldBe('Y');
        ((char)ptr[3]).ShouldBe('Z');
    }

    [Test]
    public void ToFourCC_WithoutExactlyFourChars_Throws()
    {
        Should.Throw<ArgumentException>(() => TextUtility.ToFourCc(""));
        Should.Throw<ArgumentException>(() => TextUtility.ToFourCc("a"));
        Should.Throw<ArgumentException>(() => TextUtility.ToFourCc("ab"));
        Should.Throw<ArgumentException>(() => TextUtility.ToFourCc("abc"));
        Should.Throw<ArgumentException>(() => TextUtility.ToFourCc("abcde"));
        Should.Throw<ArgumentException>(() => TextUtility.ToFourCc("abcdef"));
    }
}
