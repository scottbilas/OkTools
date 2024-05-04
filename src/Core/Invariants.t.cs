using System.Globalization;

class InvariantsTests
{
    static CultureInfo s_prevCulture = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        s_prevCulture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Thread.CurrentThread.CurrentCulture = s_prevCulture;
    }

    // default handling uses culture, but my defaults are invariant because i'm almost always doing coder stuff
    // and mostly for myself. so i want culture-sensitive work to be explicit.

    [Test]
    public void ToUpperLower_WithOppositeCase_UsesInvariant()
    {
        // system set to turkish
        char.ToUpper('i').ShouldBe('İ');
        char.ToLower('I').ShouldBe('ı');
        "ii".ToUpper().ShouldBe("İİ");
        "II".ToLower().ShouldBe("ıı");

        // mine should be invariant
        'i'.ToUpper().ShouldBe('I');
        'I'.ToLower().ShouldBe('i');
        "ii".ToUpperFirstChar().ShouldBe("Ii");
        "II".ToLowerFirstChar().ShouldBe("iI");
        new[] { "ii" }.SelectToUpper().Single().ShouldBe("II");
        new[] { "II" }.SelectToLower().Single().ShouldBe("ii");
    }

    [Test]
    public void ToUpperLower_WithTargetCase_LeavesUnmodifiedRegardlessOfCulture()
    {
        char.ToLower('i').ShouldBe('i');
        char.ToUpper('I').ShouldBe('I');
        'i'.ToLower().ShouldBe('i');
        'I'.ToUpper().ShouldBe('I');
        "iI".ToLowerFirstChar().ShouldBe("iI");
        "Ii".ToUpperFirstChar().ShouldBe("Ii");
        new[] { "ii" }.SelectToLower().Single().ShouldBe("ii");
        new[] { "II" }.SelectToUpper().Single().ShouldBe("II");
    }
}
