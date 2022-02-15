class EnumUtilityTests
{
    // ReSharper disable UnusedMember.Global UnusedMember.Local InconsistentNaming
    enum SampleEnum { ValueOne = 1, AnotherValue = 2 << 4, AThirdValue = 123, FourthValue = -123 }
    enum NonCaseSensitiveUniqueNames { Value, VALUE, value }
    // ReSharper restore UnusedMember.Global UnusedMember.Local InconsistentNaming

    [Test]
    public void GetNames_MatchesFrameworkCall()
    {
        var utilNames = EnumUtility.GetNames<SampleEnum>();
        var frameworkNames = Enum.GetNames(typeof(SampleEnum));

        utilNames.ShouldBe(frameworkNames);
    }

    [Test]
    public void GetLowercaseNames_WithCaseSensitiveUniqueNames_MatchesLowercasedFrameworkCall()
    {
        var utilNames = EnumUtility.GetLowercaseNames<SampleEnum>();
        var frameworkNames = Enum.GetNames(typeof(SampleEnum)).Select(n => n.ToLower());

        utilNames.ShouldBe(frameworkNames);
    }

    [Test]
    public void GetLowercaseNames_WithNonCaseSensitiveUniqueNames_Throws()
    {
        Should
            .Throw<Exception>(EnumUtility.GetLowercaseNames<NonCaseSensitiveUniqueNames>)
            .Message.ShouldContain("Unexpected case insensitive duplicates");
    }

    [Test]
    public void GetValues_MatchesFrameworkCall()
    {
        var utilValues = EnumUtility.GetValues<SampleEnum>();
        var frameworkValues = (SampleEnum[])Enum.GetValues(typeof(SampleEnum));

        utilValues.ShouldBe(frameworkValues);
    }
}
