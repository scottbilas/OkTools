using OkTools.Core.Terminal;

class CharUtilsTests
{
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
}
