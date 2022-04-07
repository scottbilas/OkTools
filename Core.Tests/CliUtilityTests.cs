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
}
