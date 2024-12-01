partial class TextUtilityTests
{
    [TestCase("{{macro}}", "value")]
    [TestCase("{{spaces are ok}}{{macro}}{{another}}", "yes they arevalue**result**")]
    [TestCase("xyzzy\n{{macro}}{{macro}}**more", "xyzzy\nvaluevalue**more")]
    public void ReplaceMacros_Basics(string text, string expected)
    {
        var result = TextUtility.ReplaceMacros(text, [
                ("macro", w => w.Write("value")),
                ("another", w => w.Write("**result**")),
                ("spaces are ok", w => w.Write("yes they are"))]);
        result
            .ShouldBe(expected);
    }

    [TestCase("{{")]
    [TestCase("abc {{def")]
    [TestCase("abc {{def} ghi")]
    [TestCase("abc {{valid}} {{ghi jkl} xyzzy")]
    public void ReplaceMacros_WithIncompleteMacro_Throws(string text)
    {
        Should
            .Throw<FormatException>(() =>
                TextUtility.ReplaceMacros(text, [("valid", _ => { })]))
            .Message.ShouldContain("was not closed");
    }

    [TestCase("{{macros}}")]
    [TestCase("{{amacro}}")]
    [TestCase("the {{macro}} is not {{amacro}}")]
    [TestCase("the {{macro}} is also not {{macros}}")]
    public void ReplaceMacros_WithInvalidMacro_Throws(string text)
    {
        Should
            .Throw<FormatException>(() =>
                TextUtility.ReplaceMacros(text, [("valid", _ => { })]))
            .Message.ShouldContain("Unrecognized macro");
    }

    [TestCase("abc")]
    [TestCase("abc {notamacro} def")]
    [TestCase("")]
    [TestCase(" abc  \n   foobar  ")]
    public void ReplaceMacros_WithNoMacro_ReturnsSame(string text)
    {
        var replaced = TextUtility.ReplaceMacros(text, [("xyzzy", "jooky")]);
        replaced.ShouldBe(text);

        // we should get the exact same string back if no work was done on it
        ReferenceEquals(replaced.ToString(), text).ShouldBeTrue();
    }
}
