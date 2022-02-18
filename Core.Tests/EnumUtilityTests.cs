class EnumUtilityTests
{
    // TODO: test flags enums too

    // ReSharper disable UnusedMember.Global UnusedMember.Local InconsistentNaming
    enum SampleEnum { ValueOne = 1, AnotherValue = 2 << 4, AThirdValue = 123, FourthValue = -123 }
    enum NonCaseSensitiveUniqueNames { Value, VALUE, value }
    // ReSharper restore UnusedMember.Global UnusedMember.Local InconsistentNaming

    [TestCaseGeneric(TypeArguments = new[] { typeof(SampleEnum) })]
    [TestCaseGeneric(TypeArguments = new[] { typeof(NonCaseSensitiveUniqueNames) })]
    public void GetCount_MatchesFrameworkCall<T>() where T: struct, Enum
    {
        var frameworkNames = Enum.GetNames(typeof(T));

        EnumUtility.GetCount<T>().ShouldBe(frameworkNames.Length);
    }

    [TestCaseGeneric(TypeArguments = new[] { typeof(SampleEnum) })]
    [TestCaseGeneric(TypeArguments = new[] { typeof(NonCaseSensitiveUniqueNames) })]
    public void GetNames_MatchesFrameworkCall<T>() where T: struct, Enum
    {
        var utilNames = EnumUtility.GetNames<T>();
        var frameworkNames = Enum.GetNames(typeof(T));

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

    [TestCaseGeneric(TypeArguments = new[] { typeof(SampleEnum) })]
    [TestCaseGeneric(TypeArguments = new[] { typeof(NonCaseSensitiveUniqueNames) })]
    public void GetValues_MatchesFrameworkCall<T>() where T: struct, Enum
    {
        var utilValues = EnumUtility.GetValues<T>();
        var frameworkValues = (T[])Enum.GetValues(typeof(T));

        utilValues.ShouldBe(frameworkValues);
    }

    [TestCaseGeneric(TypeArguments = new[] { typeof(SampleEnum) })]
    [TestCaseGeneric(TypeArguments = new[] { typeof(NonCaseSensitiveUniqueNames) })]
    public void TryParse_WithValidNames_ShouldReturnValues<T>() where T: struct, Enum
    {
        var frameworkNames = Enum.GetNames(typeof(T));
        var frameworkValues = (T[])Enum.GetValues(typeof(T));

        for (var i = 0; i < frameworkNames.Length; ++i)
        {
            EnumUtility.TryParse<T>(frameworkNames[i]).ShouldBe(frameworkValues[i]);
            EnumUtility.TryParseOr<T>(frameworkNames[i]).ShouldBe(frameworkValues[i]);
        }
    }

    [TestCaseGeneric(SampleEnum.FourthValue, TypeArguments = new[] { typeof(SampleEnum) })]
    [TestCaseGeneric(NonCaseSensitiveUniqueNames.VALUE, TypeArguments = new[] { typeof(NonCaseSensitiveUniqueNames) })]
    public void TryParse_WithInvalidName_ShouldReturnDefault<T>(T defValue) where T: struct, Enum
    {
        EnumUtility.TryParse(null!, defValue).ShouldBe(defValue);
        EnumUtility.TryParse("", defValue).ShouldBe(defValue);
        EnumUtility.TryParse("xyz", defValue).ShouldBe(defValue);

        EnumUtility.TryParse<T>(null!).ShouldBe(default);
        EnumUtility.TryParse<T>("").ShouldBe(default);
        EnumUtility.TryParse<T>("xyz").ShouldBe(default);

        EnumUtility.TryParseOr<T>(null!).ShouldBe(null);
        EnumUtility.TryParseOr<T>("").ShouldBe(null);
        EnumUtility.TryParseOr<T>("xyz").ShouldBe(null);
    }

    [Test]
    public void TryParseIgnoreCase_WithValidNames_ShouldReturnValues()
    {
        // note that NonCaseSensitiveUniqueNames doesn't work here. ultimately we call Enum.TryParse,
        // which just returns the first match it finds. if we take over the parsing ourselves we could
        // throw on the invalid attempt to do case-insensitive ops on a non-unique enum.

        var frameworkNames = Enum.GetNames(typeof(SampleEnum));
        var frameworkValues = (SampleEnum[])Enum.GetValues(typeof(SampleEnum));

        for (var i = 0; i < frameworkNames.Length; ++i)
        {
            EnumUtility.TryParseIgnoreCase<SampleEnum>(frameworkNames[i]).ShouldBe(frameworkValues[i]);
            EnumUtility.TryParseIgnoreCaseOr<SampleEnum>(frameworkNames[i]).ShouldBe(frameworkValues[i]);
        }
    }

    [TestCaseGeneric(SampleEnum.FourthValue, TypeArguments = new[] { typeof(SampleEnum) })]
    [TestCaseGeneric(NonCaseSensitiveUniqueNames.VALUE, TypeArguments = new[] { typeof(NonCaseSensitiveUniqueNames) })]
    public void TryParseIgnoreCase_WithInvalidName_ShouldReturnDefault<T>(T defValue) where T: struct, Enum
    {
        EnumUtility.TryParseIgnoreCase(null!, defValue).ShouldBe(defValue);
        EnumUtility.TryParseIgnoreCase("", defValue).ShouldBe(defValue);
        EnumUtility.TryParseIgnoreCase("xyz", defValue).ShouldBe(defValue);

        EnumUtility.TryParseIgnoreCase<T>(null!).ShouldBe(default);
        EnumUtility.TryParseIgnoreCase<T>("").ShouldBe(default);
        EnumUtility.TryParseIgnoreCase<T>("xyz").ShouldBe(default);

        EnumUtility.TryParseIgnoreCaseOr<T>(null!).ShouldBe(null);
        EnumUtility.TryParseIgnoreCaseOr<T>("").ShouldBe(null);
        EnumUtility.TryParseIgnoreCaseOr<T>("xyz").ShouldBe(null);
    }
}
