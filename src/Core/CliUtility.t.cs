class CliUtilityTests
{
    [Test]
    public void ParseCommandLineArgs_CommandLineArgsToString()
    {
        var text = """
            "C:\path\with space\unity.exe" -flag "Arg With Space" -another "internally \"quoted strings\" yup.."
            """;
        var items = new[]
        {
            @"C:\path\with space\unity.exe",
            "-flag",
            "Arg With Space",
            "-another",
            "internally \"quoted strings\" yup.."
        };

        var fromText = CliUtility.ParseCommandLineArgs(text).ToList();
        var fromItems = CliUtility.CommandLineArgsToString(items);

        fromItems.ShouldBe(text);
        fromText.ShouldBe(items);
    }

    [Test]
    public void ParseCommandLineArgs_CommandLineArgsToString_WithNoSpaces_StripsQuotes()
    {
        var text = "test of \"-quoted\" not needed";
        var unquoted = "test of -quoted not needed";
        var items = new[] { "test", "of", "-quoted", "not", "needed" };

        var fromText = CliUtility.ParseCommandLineArgs(text).ToList();
        var fromItems = CliUtility.CommandLineArgsToString(items);

        fromItems.ShouldBe(unquoted);
        fromText.ShouldBe(items);
    }

    [Test]
    public void ParseCommandLineArgs_WithInternalQuote()
    {
        //var text = """
        //one onemore\"quoted "another\"internal\"again"
        //""";
        var unquoted = """
        one onemore\"quoted another\"internal\"again
        """;
        var items = new[] { "one", "onemore\"quoted", "another\"internal\"again" };

        //var fromText = CliUtility.ParseCommandLineArgs(text).ToList();
        var fromItems = CliUtility.CommandLineArgsToString(items);

        fromItems.ShouldBe(unquoted);
        //fromText.ShouldBe(items);  TODO: doesn't work yet
    }

    [Test]
    public void ParseCommandLineArgs_WithEmptyOrWhitespaceString_ShouldReturnEmptyCollection()
    {
        CliUtility.ParseCommandLineArgs("").ShouldBeEmpty();
        CliUtility.ParseCommandLineArgs("    ").ShouldBeEmpty();
    }

    [Test]
    public void CommandLineArgsToString_WithEmptyOrWhitespaceStrings_ShouldReturnEmptyString()
    {
        CliUtility.CommandLineArgsToString(Array.Empty<string>()).ShouldBe("");
        CliUtility.CommandLineArgsToString(new[] { "    ", "", " " }).ShouldBe("");
    }
}
