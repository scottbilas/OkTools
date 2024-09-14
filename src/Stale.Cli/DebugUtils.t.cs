class DebugUtilsTests
{
    [TestCase("abc",         "abc")]
    [TestCase("abc;def",     "abc;def")]
    [TestCase("abc;abc;def", "abc.;def")]
    [TestCase("abc;def;abc", "abc;def;abc")]
    [TestCase("abc;def;def;abc", "abc;def.;abc")]
    [TestCase("abc;def;def;def;def;abc", "abc;def...;abc")]
    [TestCase("abc;def;abc;abc;abc", "abc;def;abc..")]
    public void SequenceDuplicatesAsDots(string given, string expected)
    {
        var givenArray = given.Split(';');
        var expectedArray = expected.Split(';');

        DebugUtils.SequenceDuplicatesAsDots(givenArray).ShouldBe(expectedArray);
    }

    [Test]
    public void SequenceDuplicatesAsDots_WithEmpties()
    {
        DebugUtils.SequenceDuplicatesAsDots([]).ShouldBe([]);
    }
}
