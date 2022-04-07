class StringUtilityTests
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
}
