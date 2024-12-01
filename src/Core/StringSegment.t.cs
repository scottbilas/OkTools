class StringSegmentTests
{
    [TestCase("abc", "abc", 0, true)]
    [TestCase("abcd", "abc", 1, false)]
    [TestCase("", "", 0, true)]
    public void Equals(string a, string b, int compare, bool equal)
    {
        var s = a.AsStringSegment();

        s.Compare(b).ShouldBe(compare);
        s.Equals(b).ShouldBe(equal);

        s.Compare(b.AsStringSegment()).ShouldBe(compare);
        s.Equals(b.AsStringSegment()).ShouldBe(equal);

        s.Compare(b.AsSpan()).ShouldBe(compare);
        s.Equals(b.AsSpan()).ShouldBe(equal);

        s.Compare(b.AsMemory()).ShouldBe(compare);
        s.Equals(b.AsMemory()).ShouldBe(equal);
    }

    [TestCase("", -1, 5, "")]
    [TestCase("abc", -1, 5, "abc")]
    [TestCase("abcdefgh", -1, 5, "abcd")]
    [TestCase("", 0, 15, "")]
    [TestCase("abc", 1, 15, "bc")]
    [TestCase("abcdefgh", 2, 15, "cdefgh")]
    [TestCase("", 0, 0, "")]
    [TestCase("abc", 1, 1, "b")]
    [TestCase("abcdefgh", 2, 5, "cdefg")]
    public void SliceSafe_WithStartAndLength(string str, int start, int length, string expected)
    {
        var result = str.AsStringSegment().SliceSafe(start, length);
        result.ToString().ShouldBe(expected);
    }

    [TestCase("", -1, "")]
    [TestCase("abc", -1, "abc")]
    [TestCase("abcdefgh", -1, "abcdefgh")]
    [TestCase("", 0, "")]
    [TestCase("abc", 1, "bc")]
    [TestCase("abcdefgh", 2, "cdefgh")]
    public void SliceSafe_WithStart(string str, int start, string expected)
    {
        var result = str.AsStringSegment().SliceSafe(start);
        result.ToString().ShouldBe(expected);
    }
}
