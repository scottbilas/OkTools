class DocoptUtilityTests
{
    static string Reflow(string text, int width, int minWrapWidth = 0, string eol = "\n") =>
        DocoptUtility.Reflow(text, width,
            new DocoptReflowOptions
            {
                MinWrapWidth = minWrapWidth,
                IndentFallback = 0,
                Eol = eol,
            });

    [Test]
    public void Reflow_EmptyLines()
    {
        Reflow(
            "\n\r\n\n", 50).ShouldBe(
            "\n\n\n");
        Reflow(
            "", 50).ShouldBe(
            "");
    }

    [Test]
    public void Reflow_SoftWrap()
    {
        Reflow(
            "this is a small sentence", 50).ShouldBe(
            "this is a small sentence");
        Reflow(
            "this is a small sentence", 18).ShouldBe(
            "this is a small\nsentence");
        Reflow(
            "this is a small sentence", 12).ShouldBe(
            "this is a\nsmall\nsentence");
    }

    [Test]
    public void Reflow_WithOverflow_DoesHardWrap()
    {
        Reflow(
            "this has a really_long_word_in_it_yes_it_does", 12).ShouldBe(
            "this has a\nreally_long_\nword_in_it_y\nes_it_does");
    }

    [Test]
    public void Reflow_WithHardWrap()
    {
        Reflow(
            "this has a difficult to wrap word in it", 9, 4).ShouldBe(
            "this has\na difficu\nlt to\nwrap word\nin it");
        Reflow(
            "this has a difficult to wrap word in it", 10, 4).ShouldBe(
            "this has a\ndifficult\nto wrap\nword in it");
    }

    [Test]
    public void Reflow_WithSimpleContinuations()
    {
        Reflow(
            "this is a line we are wrapping\nand another that should be joined to it", 12).ShouldBe(
            "this is a\nline we are\nwrapping and\nanother that\nshould be\njoined to it");
        Reflow(
            "this is a line we are wrapping and\nanother that should be joined to it", 12).ShouldBe(
            "this is a\nline we are\nwrapping and\nanother that\nshould be\njoined to it");
    }

    [Test]
    public void Reflow_WithExistingWhitespaceBreaks()
    {
        // baseline
        Reflow(
            "this is a line we r wrapping", 12).ShouldBe(
            "this is a\nline we r\nwrapping");
        Reflow(
            "this is a1 line we r wrapping", 12).ShouldBe(
            "this is a1\nline we r\nwrapping");
        Reflow(
            "this is a12 line we r wrapping", 12).ShouldBe(
            "this is a12\nline we r\nwrapping");
        Reflow(
            "this is a123 line we r wrapping", 12).ShouldBe(
            "this is a123\nline we r\nwrapping");

        // collapse whitespace when wrapped
        Reflow(
            "* this is a      line we r     wrapping   ", 14).ShouldBe(
            "* this is a\n  line we r\n  wrapping");

        // preserve whitespace when not wrapped
        Reflow(
            "- this is a   line we r     wrapping   ", 20).ShouldBe(
            "- this is a   line\n  we r     wrapping");
    }

    [Test]
    public void Reflow_WithSimpleIndentation()
    {
        Reflow(
            "this line\nshould join", 50).ShouldBe(
            "this line should join");
        Reflow(
            "  this line\n  should join", 50).ShouldBe(
            "  this line should join");

        Reflow(
            "  this line\n  should not join", 17).ShouldBe(
            "  this line\n  should not join");
        Reflow(
            "  this line\n  should join", 18).ShouldBe(
            "  this line should\n  join");
        Reflow(
            "  this line\n  should join", 22).ShouldBe(
            "  this line should\n  join");
        Reflow(
            "  this line\n  should join", 23).ShouldBe(
            "  this line should join");
    }

    [Test]
    public void Reflow_WithInterruptedIndentation()
    {
        Reflow(
            "this line\n\nshould not join", 50).ShouldBe(
            "this line\n\nshould not join");
        Reflow(
            "  this line\n\n  should not join", 50).ShouldBe(
            "  this line\n\n  should not join");
        Reflow(
            "  this line\n  \n  should not join", 50).ShouldBe(
            "  this line\n\n  should not join");

        Reflow(
            "  this line\n should not join\n ok but this\n  but no", 50).ShouldBe(
            "  this line\n should not join ok but this\n  but no");
    }

    [Test]
    public void Reflow_WithSecondIndentation()
    {
        Reflow(
            "  option  description\n          and join", 23).ShouldBe(
            "  option  description\n          and join");
        Reflow(
            "  option  description\n          and join", 25).ShouldBe(
            "  option  description and\n          join");
        Reflow(
            "  option  description\n          and join", 50).ShouldBe(
            "  option  description and join");
    }

    [Test]
    public void Reflow_WithBulletPoints()
    {
        Reflow(
            "here is\n  * one point\n    and this won't! join", 16).ShouldBe(
            "here is\n  * one point\n    and this\n    won't! join");
        Reflow(
            "here is\n  * one point\n    and this won't! join", 18).ShouldBe(
            "here is\n  * one point and\n    this won't!\n    join");
        Reflow(
            "here is\n  * one point\n    and this should join", 50).ShouldBe(
            "here is\n  * one point and this should join");
    }

    [Test]
    public void Reflow_Eols()
    {
        Reflow(
            "some text\nother text\r\nstill more", 10).ShouldBe(
            "some text\nother text\nstill more");
        Reflow(
            "some text\nother text\r\nstill more", 10, eol: "\r\n").ShouldBe(
            "some text\r\nother text\r\nstill more");
    }

    [Test]
    public void Reflow_TrimLineEnds()
    {
        Reflow(
            "abc \n def ", 50).ShouldBe(
            "abc\n def");
    }

    [Test]
    public void Reflow_LongIndentedWord_ShouldNotBreakOnLeadingWhitespace()
    {
        Reflow(
            "    this_is_a_really_long_word", 15).ShouldBe(
            "    this_is_a_r\n    eally_long_\n    word");
    }
}
