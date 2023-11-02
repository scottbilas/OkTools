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

        Reflow(
            "here is\n  - one point\n    and this won't! join", 16).ShouldBe(
            "here is\n  - one point\n    and this\n    won't! join");
        Reflow(
            "here is\n  - one point\n    and this won't! join", 18).ShouldBe(
            "here is\n  - one point and\n    this won't!\n    join");
        Reflow(
            "here is\n  - one point\n    and this should join", 50).ShouldBe(
            "here is\n  - one point and this should join");
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

    [Test, Category("Regression")]
    public void Reflow_FinalLineIndented()
    {
        // before the fix, this would fail to indent the very last line

        Reflow(
            "Usage:\n"+
            "  okflog  PATH long stuff on top of it\n"+
            "  okflog  --version long stuff on top of it",
            32).ShouldBe(
            "Usage:\n"+
            "  okflog  PATH long stuff on top\n"+
            "          of it\n"+
            "  okflog  --version long stuff\n"+
            "          on top of it");
    }

    [Test]
    public void Reflow_WithProgramUsage_DoesNotJoinLines()
    {
        Reflow(
            "Usage:\n"+
            "  loggo  [options] [DESTINATION]\n"+
            "  loggo  --version",
            50).ShouldBe(
            "Usage:\n"+
            "  loggo  [options] [DESTINATION]\n"+
            "  loggo  --version");
    }

    [Test]
    public void Reflow_WithProgramUsage_WrapsLinesIndividually()
    {
        // this tests out the section "leading word" detection and extra indent adjusting

        Reflow(
            "Usage:\n"+
            "  progname and some extra text\n"+
            "  progname and some other text",
            23).ShouldBe(
            "Usage:\n"+
            "  progname and some\n"+
            "           extra text\n"+
            "  progname and some\n"+
            "           other text");
    }

    [Test]
    public void Reflow_WithDoubleSpace_PrefersIndentAtDoubleSpace()
    {
        Reflow(
            "Options:\n"+
            "  --thingy  This is a really long line that should be wrapping at the double space\n"+
            "    * first:   This one should not wrap at '* ' but instead at 'This'\n"+
            "    * second:  This one should also wrap at the 'This' and not before that\n",
            50).ShouldBe(
            "Options:\n"+
            "  --thingy  This is a really long line that should\n"+
            "            be wrapping at the double space\n"+
            "    * first:   This one should not wrap at '* '\n"+
            "               but instead at 'This'\n"+
            "    * second:  This one should also wrap at the\n"+
            "               'This' and not before that");
    }
}
