partial class TextUtilityTests
{
    static string ReplaceMacros(string source, params (string name, Action<TextWriter> replacer)[] replacements)
    {
        var dictReplacer = TextUtility.CreateMacroReplacer(0, replacements);
        var arrayReplacer = TextUtility.CreateMacroReplacer(1000, replacements);

        // check the two types of replacers work the same
        var dictResult = TextUtility.ReplaceMacros(source, dictReplacer);
        var arrayResult = TextUtility.ReplaceMacros(source, arrayReplacer);
        arrayResult.ShouldBe(dictResult);

        return dictResult;
    }

    [TestCase("{{macro}}", "value")]
    [TestCase("{{spaces are ok}}{{macro}}{{another}}", "yes they arevalue**result**")]
    [TestCase("xyzzy\n{{macro}}{{macro}}**more", "xyzzy\nvaluevalue**more")]
    public void ReplaceMacros_Basics(string text, string expected)
    {
        ReplaceMacros(text,
                ("macro", w => w.Write("value")),
                ("another", w => w.Write("**result**")),
                ("spaces are ok", w => w.Write("yes they are")))
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
                ReplaceMacros(text, ("valid", _ => {})))
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
                ReplaceMacros(text, ("valid", _ => {})))
            .Message.ShouldContain("Unrecognized macro");
    }

    [TestCase("abc")]
    [TestCase("abc {notamacro} def")]
    [TestCase("")]
    [TestCase(" abc  \n   foobar  ")]
    public void ReplaceMacros_WithNoMacro_ReturnsSame(string text)
    {
        ReplaceMacros(text).ShouldBe(text);
    }
}
