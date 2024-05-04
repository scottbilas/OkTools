using System.Globalization;

class CharUtilsTests
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

    [TestCase('\a', "\\a")]
    [TestCase('\r', "\\r")]
    [TestCase((char)0x7f, "\\b")]
    [TestCase((char)0x1b, "^[")]
    [TestCase('Q', "Q")]
    [TestCase('[', "[")]
    [TestCase('z', "z")]
    [TestCase('İ', "\\u0130")]
    public void ToNiceString(char ch, string expected)
    {
        CharUtils.ToNiceString(ch).ShouldBe(expected);
    }

    [TestCase('\xf',    "^O",       "^O")] // shrug
    [TestCase('\xf2',   "\\xf2",    "\\u00f2")]
    [TestCase('\xf23',  "\\u0f23",  "\\u0f23")]
    [TestCase('\xf234', "\\uf234",  "\\uf234")]
    [TestCase('\x41b',  "\\u041b",  "\\u041b")]
    public void ToNiceString_WithAmbiguousHexEscape(char ch, string expectedx, string expectedu)
    {
        CharUtils.ToNiceString(ch).ShouldBe(expectedx);
        CharUtils.ToNiceString(ch, true).ShouldBe(expectedu);
    }

    // default handling uses culture, but my defaults are invariant because i'm almost always doing coder stuff
    // and mostly for myself. so i want culture-sensitive work to be explicit.

    [Test]
    public void ToUpperLower_WithOppositeCase_UsesInvariant()
    {
        // system set to turkish
        char.ToUpper('i').ShouldBe('İ');
        char.ToLower('I').ShouldBe('ı');

        // mine should be invariant
        'i'.ToUpper().ShouldBe('I');
        'I'.ToLower().ShouldBe('i');
    }

    [Test]
    public void ToUpperLower_WithTargetCase_LeavesUnmodifiedRegardlessOfCulture()
    {
        char.ToLower('i').ShouldBe('i');
        char.ToUpper('I').ShouldBe('I');
        'i'.ToLower().ShouldBe('i');
        'I'.ToUpper().ShouldBe('I');
    }
}
