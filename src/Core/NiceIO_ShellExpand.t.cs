class NiceIOShellExpandTests
{
    [OneTimeSetUp]
    public void Setup()
    {
        Environment.SetEnvironmentVariable("niceio_test_a", "testa");
        Environment.SetEnvironmentVariable("NICEIO_TEST_B", "TESTB");
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("niceio_test_a", null);
        Environment.SetEnvironmentVariable("NICEIO_TEST_B", null);
    }

    [TestCase("", ".")]
    [TestCase("$niceio_test_a", "testa")]
    [TestCase("%niceio_test_a%", "testa")]
    [TestCase("~/$niceio_test_a", "~/testa")]
    [TestCase("~/%niceio_test_a%", "~/testa")]
    [TestCase("$niceio_test_b", "TESTB")] // maybe should be windows-only?
    [TestCase("%niceio_test_b%", "TESTB")] // maybe should be windows-only?
    [TestCase("$NICEIO_TEST_B", "TESTB")]
    [TestCase("%NICEIO_TEST_B%", "TESTB")]
    [TestCase("foo.$NICEIO_TEST_B/bar$niceio_test_a$$", "foo.TESTB/bartesta$$")]
    [TestCase("foo.%NICEIO_TEST_B%/bar$niceio_test_a$$", "foo.TESTB/bartesta$$")]
    public void ShellExpand_Replaced(string path, string expected)
    {
        var expanded = path.ToNPath().ShellExpand();
        expanded.ShouldBe(expected.ToNPath().TildeExpand());
    }

    [TestCase("$")]
    [TestCase("%")]
    [TestCase("%%")]
    [TestCase("$niceio_test_c")]
    [TestCase("%niceio_test_c%")]
    [TestCase("foo$niceio_test_abar")]
    [TestCase("foo%niceio_test_abar%")]
    [TestCase("~$niceio_test_abar")]
    [TestCase("~%niceio_test_abar%")]
    public void ShellExpand_Same(string path)
    {
        var expanded = path.ToNPath().ShellExpand();
        expanded.ToString().ShouldBe(path);
    }
}
