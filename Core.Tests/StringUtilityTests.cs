class StringUtilityTests
{
    [Test]
    public void DetectEolType_WithStopAfterWindow_ReturnsMatchAfterStopping()
    {
        StringUtility.DetectEolType("\r\n \n \n", 0).ShouldBe("\n");
        StringUtility.DetectEolType("\r\n \n \n", 1).ShouldBe("\r\n");
        StringUtility.DetectEolType("\r\n \n \n", 2).ShouldBe("\n");
        StringUtility.DetectEolType("\r\n \n \n \n \r\n \r\n", 2).ShouldBe("\n");
    }

    [Test]
    public void DetectEolType_WithNoEols_ReturnsSystemEol()
    {
        StringUtility.DetectEolType("").ShouldBe(Environment.NewLine);
        StringUtility.DetectEolType("abc").ShouldBe(Environment.NewLine);
    }

    [Test]
    public void DetectEolType_WithBasicEols_MatchesExpected()
    {
        StringUtility.DetectEolType("\n").ShouldBe("\n");
        StringUtility.DetectEolType("\n \n").ShouldBe("\n");
        StringUtility.DetectEolType("\r\n").ShouldBe("\r\n");
        StringUtility.DetectEolType("\r\n \r\n").ShouldBe("\r\n");
        StringUtility.DetectEolType("\r").ShouldBe("\n");
        StringUtility.DetectEolType("\r \r").ShouldBe("\n");
    }

    [Test]
    public void DetectEolType_WithAmbiguousEols_ReturnsSystemEol()
    {
        StringUtility.DetectEolType("\r\n \n").ShouldBe(Environment.NewLine);
        StringUtility.DetectEolType("\n \r\n").ShouldBe(Environment.NewLine);
        StringUtility.DetectEolType("\n \r\n \r\n \n").ShouldBe(Environment.NewLine);
    }

    [Test]
    public void DetectEolType_WithMismatchedEols_ReturnsMoreCommonEol()
    {
        StringUtility.DetectEolType("\r\n \r\n \n").ShouldBe("\r\n");
        StringUtility.DetectEolType("\n \r\n \n").ShouldBe("\n");
    }
}
