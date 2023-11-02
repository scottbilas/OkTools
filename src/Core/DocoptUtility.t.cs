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

    [Test, Category("TODO")]
    public void Reflow_Bug_FinalLineNotIndented()
    {
        // TODO: something about the '--version' in there is causing the bad wrapping
        Reflow(
            "Usage:\n"+
            "  okflog  PATH long stuff on top of it\n"+
            "  okflog  --version long stuff on top of it",
            32).ShouldBe(
            "Usage:\n"+
            "  okflog  PATH long stuff on top\n"+
            "          of it\n"+
        //  WHAT WE WANT
        //  "  okflog  --version long stuff on\n"+
        //  "          top of it"
        //  WHAT WE ACTUALLY GET
            "  okflog  --version long stuff\n"+
            "  on top of it"
            );
    }

    [Test, Category("TODO"), Ignore("need bugfix, hacked around for now")]
    public void Reflow_WithProgramUsage_DoesNotJoinLines()
    {
        // TODO: once this bug is resolved, remove the hack from DocoptUtility.SelectSections, and also update --no-hub to be like below formatting

        // TODO: something about the '--version' in there is causing the bad wrapping
        // UPDATE: this is probably caused by the bracket. changing "[options]" to "options" probably won't show the problem.
        //         i ran into this with a "[windows-only]" prefix on some options help text and removing the brackets fixed it. same issue happens with parens..
        //         so it's something easy with the `\b` in s_indentRx that's causing the problem.
        //  --no-hub                [windows-only] Run `okunity do hidehub --kill-hub` before launching Unity, which will kill the Hub if running and also prevent the auto-launch of the Hub that Unity does (note that this change has global impact, check `help do` for more info on this)

        Reflow(
            "Usage:\n"+
            "  loggo  [options] [DESTINATION]\n"+
            "  loggo  --version\n",
            50).ShouldBe(
        //  WHAT WE WANT
        //  "Usage:\n"+
        //  "  loggo  [options] [DESTINATION]\n"+
        //  "  loggo  --version",
        //  WHAT WE ACTUALLY GET (without the hack)
            "Usage:\n"+
            "  loggo  [options] [DESTINATION] loggo  --version"
            );

        // these tests worked before i hacked out the "usage" wrapping.

        // (previously i could double-space after the program name to work around the issue, but that started failing
        // at some point, leading to the above false test.)

        #if NO
        Reflow(
            "Usage:\n  progname and some extra text\n  progname and some other text", 23).ShouldBe(
            // TODO: what i want to work
            //  "Usage:\n  progname and some\n           extra text\n  progname and some\n           other text");
            // what currently happens
            "Usage:\n  progname and some\n  extra text progname\n  and some other text");

        // this gets it right, but requires a workaround of double-space after program name (not the end of the world)
        Reflow(
            "Usage:\n  progname  and some extra text\n  progname  and some other text", 23).ShouldBe(
            "Usage:\n  progname  and some\n            extra text\n  progname  and some\n            other text");
        #endif

    }

    [Test, Category("TODO")]
    public void Reflow_WithDoubleSpace_IndentsAtDoubleSpace()
    {
        Reflow(
            "Options:\n"+
            "  --thingy  This is a really long line that should be wrapping at the double space\n"+
            "    * first:   This one should not wrap at '* ' but instead at 'This'\n"+
            "    * second:  This one should also wrap at the 'This' and not before that\n",
            50).ShouldBe(
        // WHAT WE WANT
        //  "Options:\n"+
        //  "  --thingy  This is a really long line that should\n"+
        //  "            be wrapping at the double space\n"+
        //  "    * first:   This one should not wrap at '* '\n"+
        //  "               but instead at 'This'\n"+
        //  "    * second:  This one should also wrap at the\n"+
        //  "               'This' and not before that");
        // WHAT WE ACTUALLY GET
            "Options:\n"+
            "  --thingy  This is a really long line that should\n"+
            "            be wrapping at the double space\n"+
            "    * first:   This one should not wrap at '* '\n"+
            "      but instead at 'This'\n"+
            "    * second:  This one should also wrap at the\n"+
            "      'This' and not before that");
    }
}
