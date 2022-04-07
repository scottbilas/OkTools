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
            "some text\nother text\r\nstill more", 10).ShouldBe(
            "some text\nother text\nstill more");
        CliUtility.Reflow(
            "some text\nother text\r\nstill more", 10, eol:"\r\n").ShouldBe(
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
            "  this line\n  should not join", 17).ShouldBe(
            "  this line\n  should not join");
        CliUtility.Reflow(
            "  this line\n  should join", 18).ShouldBe(
            "  this line should\n  join");
        CliUtility.Reflow(
            "  this line\n  should join", 22).ShouldBe(
            "  this line should\n  join");
        CliUtility.Reflow(
            "  this line\n  should join", 23).ShouldBe(
            "  this line should join");
    }

    [Test]
    public void Reflow_WithInterruptedIndentation()
    {
        CliUtility.Reflow(
            "this line\n\nshould not join", 50).ShouldBe(
            "this line\n\nshould not join");
        CliUtility.Reflow(
            "  this line\n\n  should not join", 50).ShouldBe(
            "  this line\n\n  should not join");
        CliUtility.Reflow(
            "  this line\n  \n  should not join", 50).ShouldBe(
            "  this line\n\n  should not join");

        CliUtility.Reflow(
            "  this line\n should not join\n ok but this\n  but no", 50).ShouldBe(
            "  this line\n should not join ok but this\n  but no");
    }

    [Test]
    public void Reflow_WithSecondIndentation()
    {
        CliUtility.Reflow(
            "  option  description\nand join", 23).ShouldBe(
            "  option  description\n          and join");
        CliUtility.Reflow(
            "  option  description\nand join", 25).ShouldBe(
            "  option  description and\n          join");
        CliUtility.Reflow(
            "  option  description\nand join", 50).ShouldBe(
            "  option  description and join");
    }

    [Test]
    public void Reflow_WithBulletPoints()
    {
        CliUtility.Reflow(
            "here is\n  * one point\n    and this should join", 16).ShouldBe(
            "here is\n  * one point\n    and this\n    should join");
        CliUtility.Reflow(
            "here is\n  * one point\n    and this should join", 18).ShouldBe(
            "here is\n  * one point and\n    this should\n    join");
        CliUtility.Reflow(
            "here is\n  * one point\n    and this should join", 50).ShouldBe(
            "here is\n  * one point and this should join");
    }

    [Test]
    public void TestIt()
    {
        CliUtility.Reflow(
            "  Run Unity to open the given PROJECT (defaults to '.'). Uses the project version given to find the matching", 60).ShouldBe(
            "  Run Unity to open the given PROJECT (defaults to '.').\n  Uses the project version given to find the matching");


        var text =
@"Usage: okunity unity [options] [PROJECT] [-- EXTRA...]

Description:
  Run Unity to open the given PROJECT (defaults to '.'). Uses the project version given to find the matching
  Unity toolchain; see `okunity help toolchains` for how toolchains are discovered.

  Any arguments given in EXTRA will be passed along to the newly launched Unity process.

Options:
  --dry-run               Don't change anything, only print out what would happen instead
  --toolchain TOOLCHAIN   Ignore project version and use this toolchain (can be full/partial version, path to toolchain, or unityhub link)
  --scene SCENE           After loading the project, also load this specific scene (creates or overwrites {UnityProjectConstants.LastSceneManagerSetupPath.ToNPath().ToNiceString()})
  --rider                 Open Rider after the project opens in Unity
  --enable-debugging      Enable managed code debugging (disable optimizations)
  --wait-attach-debugger  Unity will pause with a dialog so you can attach a debugger
  --enable-coverage       Enable Unity code coverage
  --stack-trace-log TYPE  Override Unity settings to use the given stack trace level for logs (TYPE can be None, ScriptOnly, or Full)
  --no-local-log          Disable local log feature; Unity will use global log ({UnityConstants.UnityEditorDefaultLogPath.ToNPath().ToNiceString()})
  --no-burst              Completely disable Burst
  --no-activate-existing  Don't activate an existing Unity main window if found running on the project
  --verbose-upm-logs      Tell Unity Package Manager to write verbose logs ({UnityConstants.UpmLogPath.ToNPath().ToNiceString()})

  All of these options will only apply to the new Unity session being launched.

Log Files:
  Unity is always directed to include timestamps in its log files, which will have this format:
    <year>-<month>-<day>T<hour>:<minute>:<second>.<milliseconds>Z|<thread-id>|<log-line>
  The timestamp is unfortunately recorded in unadjusted UTC.

  Also, unless --no-local-log is used, Unity log files will be stored as `Logs/<ProjectName>-editor.log` local to the
  project. Any existing file with this name will be rotated out to a filename with a timestamp appended to it, thus
  preserving logs from previous sessions.

Debugging:
  If using --wait-attach-debugger, Unity will pause twice during startup to allow you to attach a debugger if you want,
  showing a messagebox and waiting for you to click OK. These messageboxes will pop up if you use this flag:

  1. Unity is ready for a native debugger to be attached
  2. Mono has started up and Unity is ready for a managed debugger to be attached. This is a good way to catch static
     constructors, [InitializeOnLoadMethod], etc.

  If you will be attaching a managed debugger, be sure to also select --enable-debugging.
";

        Console.WriteLine(CliUtility.Reflow(text, 60).Left(200));
    }
}
