class CliUtilityTests
{
    [Test]
    public void ParseCommandLineArgs_WithMixed_ShouldReturnAllUnquotedArgs()
    {
        var parsed = CliUtility.ParseCommandLineArgs(
            @"""C:\path\with space\unity.exe"" -flag ""-quoted"" ""Arg With Space"" -another ""one last with space""");

        parsed.ShouldBe(new[]
        {
            @"C:\path\with space\unity.exe",
            "-flag",
            "-quoted",
            "Arg With Space",
            "-another",
            "one last with space"
        });
    }

    [Test]
    public void ParseCommandLineArgs_WithEmptyOrWhitespaceString_ShouldReturnEmptyCollection()
    {
        CliUtility.ParseCommandLineArgs("").ShouldBeEmpty();
        CliUtility.ParseCommandLineArgs("    ").ShouldBeEmpty();
    }

    [Test]
    public void CommandLineArgsToString_WithMixed_ShouldReturnAllUnquotedArgs()
    {
        var argsStr = CliUtility.CommandLineArgsToString(new[]
        {
            @"C:\path\with space\unity.exe",
            "-flag",
            "-quoted",
            "Arg With Space",
            "-another",
            "one last with space"
        });

        argsStr.ShouldBe(@"""C:\path\with space\unity.exe"" -flag -quoted ""Arg With Space"" -another ""one last with space""");
    }

    [Test]
    public void CommandLineArgsToString_WithEmptyOrWhitespaceStrings_ShouldReturnEmptyString()
    {
        CliUtility.CommandLineArgsToString(Array.Empty<string>()).ShouldBe("");
        CliUtility.CommandLineArgsToString(new[] { "    ", "", " " }).ShouldBe("");
    }

    [Test]
    public void Reflow_Eols()
    {
        CliUtility.Reflow(
            "some text\nother text\r\nstill more", 50).ShouldBe(
            "some text\nother text\nstill more");
        CliUtility.Reflow(
            "some text\nother text\r\nstill more", 50, eol:"\r\n").ShouldBe(
            "some text\r\nother text\r\nstill more");
    }

    [Test]
    public void Reflow_TrimLineEnds()
    {
        CliUtility.Reflow(
            "abc \n def ", 50).ShouldBe(
            "abc\n def");
    }

    [Test]
    public void Reflow_EmptyLines()
    {
        CliUtility.Reflow(
            "\n\r\n\n", 50).ShouldBe(
            "\n\n\n");
        CliUtility.Reflow(
            "", 50).ShouldBe(
            "");
    }

    [Test]
    public void Reflow_SoftWrap()
    {
        CliUtility.Reflow(
            "this is a small sentence", 50).ShouldBe(
            "this is a small sentence");
        CliUtility.Reflow(
            "this is a small sentence", 18).ShouldBe(
            "this is a small\nsentence");
        CliUtility.Reflow(
            "this is a small sentence", 12).ShouldBe(
            "this is a\nsmall\nsentence");
    }

    [Test]
    public void Reflow_WithOverflow_DoesHardWrap()
    {
        CliUtility.Reflow(
            "this has a really_long_word_in_it_yes_it_does", 12).ShouldBe(
            "this has a\nreally_long_\nword_in_it_y\nes_it_does");
    }

    [Test]
    public void Reflow_WithHardWrap()
    {
        CliUtility.Reflow(
            "this has a difficult to wrap word in it", 9, 4).ShouldBe(
            "this has\na difficu\nlt to\nwrap word\nin it");
        CliUtility.Reflow(
            "this has a difficult to wrap word in it", 10, 4).ShouldBe(
            "this has a\ndifficult\nto wrap\nword in it");
    }

    [Test]
    public void Reflow_WithSimpleContinuations()
    {
        CliUtility.Reflow(
            "this is a line we are wrapping\nand another that should be joined to it", 12).ShouldBe(
            "this is a\nline we are\nwrapping and\nanother that\nshould be\njoined to it");
        CliUtility.Reflow(
            "this is a line we are wrapping and\nanother that should be joined to it", 12).ShouldBe(
            "this is a\nline we are\nwrapping and\nanother that\nshould be\njoined to it");
    }

    [Test]
    public void Reflow_WithExistingWhitespaceBreaks()
    {
        // baseline
        CliUtility.Reflow(
            "this is a line we r wrapping", 12).ShouldBe(
            "this is a\nline we r\nwrapping");
        CliUtility.Reflow(
            "this is a1 line we r wrapping", 12).ShouldBe(
            "this is a1\nline we r\nwrapping");
        CliUtility.Reflow(
            "this is a12 line we r wrapping", 12).ShouldBe(
            "this is a12\nline we r\nwrapping");
        CliUtility.Reflow(
            "this is a123 line we r wrapping", 12).ShouldBe(
            "this is a123\nline we r\nwrapping");

        // collapse whitespace when wrapped
        CliUtility.Reflow(
            "this is a      line we r     wrapping   ", 12).ShouldBe(
            "this is a\nline we r\nwrapping");

        // preserve whitespace when not wrapped
        CliUtility.Reflow(
            "this is a   line we r     wrapping   ", 18).ShouldBe(
            "this is a   line\nwe r     wrapping");
    }

    [Test]
    public void Reflow_WithSimpleIndentation()
    {
        CliUtility.Reflow(
            "this line\nshould join", 50).ShouldBe(
            "this line should join");
        CliUtility.Reflow(
            "  this line\n  should join", 50).ShouldBe(
            "  this line should join");
        CliUtility.Reflow(
            "  this line\n  should join", 16).ShouldBe(
            "  this line should\n  join");
    }

    [Test]
    public void Reflow_WithInterruptedIndentation()
    {
        CliUtility.Reflow(
            "this line\n\nshould join", 50).ShouldBe(
            "this line\n\nshould join");
        CliUtility.Reflow(
            "  this line\n\n  should join", 50).ShouldBe(
            "  this line\n\n  should join");
        CliUtility.Reflow(
            "  this line\n  \n  should join", 50).ShouldBe(
            "  this line should join");

        CliUtility.Reflow(
            "  this line\n should not join\n ok but this\n  but no", 50).ShouldBe(
            "  this line\n should not join ok but this\n  but no");
    }
}
