[Parallelizable]
class StringExtensionsSplitTests
{
    [TestCase("a,b,c", ",", new[] {"a","b","c"})]
    [TestCase(" a,b ,c   ", ",", new[] {"a","b","c"})]
    [TestCase("a,,c", ",", new[] {"a","","c"})]
    [TestCase("a,    ,  c", ",", new[] {"a","","c"})]
    [TestCase("    ", ",", new string[] {""})]
    public void SplitTrim(string test, string split, string[] expected)
    {
        test.SplitTrim(split).ShouldBe(expected);
    }

    [TestCase("a,b,c", ",", new[] {"a","b","c"})]
    [TestCase(" a,b ,c   ", ",", new[] {"a","b","c"})]
    [TestCase("a,,c", ",", new[] {"a","c"})]
    [TestCase("a,    ,  c", ",", new[] {"a","c"})]
    [TestCase("    ", ",", new string[] {})]
    public void SplitTrimRemoveEmpty(string test, string split, string[] expected)
    {
        test.SplitTrimRemoveEmpty(split).ShouldBe(expected);
    }

    [Test]
    public void SplitN_WithInsufficient_Throws()
    {
        Should.Throw<ArgumentException>(() => "".Split2(','));
        Should.Throw<ArgumentException>(() => "".Split3(','));
        Should.Throw<ArgumentException>(() => "".Split4(','));
        Should.Throw<ArgumentException>(() => "".Split5(','));

        Should.Throw<ArgumentException>(() => "a".Split2(','));
        Should.Throw<ArgumentException>(() => "a,b".Split3(','));
        Should.Throw<ArgumentException>(() => "a,b,c".Split4(','));
        Should.Throw<ArgumentException>(() => "a,b,c,d".Split5(','));
    }

    [Test]
    public void SplitN()
    {
        "a,b,c,d,e,f".Split2(',').ShouldBe(("a", "b,c,d,e,f"));
        "a,b,c,d,e,f".Split3(',').ShouldBe(("a", "b", "c,d,e,f"));
        "a,b,c,d,e,f".Split4(',').ShouldBe(("a", "b", "c", "d,e,f"));
        "a,b,c,d,e,f".Split5(',').ShouldBe(("a", "b", "c", "d", "e,f"));
    }

    [Test]
    public void SplitParseN_WithParseFail_Throws()
    {
        Should.Throw<FormatException>(() => "-1,abc".SplitParse2<int, float>(','));
    }

    [Test]
    public void SplitParseN()
    {
        "-1,2.4"
            .SplitParse2<int, float>(',')
            .ShouldBe((-1, 2.4f));
        "-1,2.4,c"
            .SplitParse3<int, float, char>(',')
            .ShouldBe((-1, 2.4f, 'c'));
        "-1,2.4,c,21-may"
            .SplitParse4<int, float, char, DateTime>(',')
            .ShouldBe((-1, 2.4f, 'c', DateTime.Parse("21-may")));
        "-1,2.4,c,21-may,hi"
            .SplitParse5<int, float, char, DateTime, string>(',')
            .ShouldBe((-1, 2.4f, 'c', DateTime.Parse("21-may"), "hi"));
    }
}
