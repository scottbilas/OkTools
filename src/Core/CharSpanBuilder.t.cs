class CharSpanBuilderTests
{
    [Test]
    public void AppendTrunc()
    {
        var csb = new CharSpanBuilder(10);
        csb.Append("abcd");
        csb.ToString().ShouldBe("abcd");
        csb.UnusedLength.ShouldBe(6);

        csb.AppendTrunc("efg");
        csb.ToString().ShouldBe("abcdefg");
        csb.UnusedLength.ShouldBe(3);

        csb.AppendTrunc("hijklmno");
        csb.ToString().ShouldBe("abcdefghij");
        csb.UnusedLength.ShouldBe(0);

        csb.AppendTrunc("a");
        csb.ToString().ShouldBe("abcdefghij");
        csb.UnusedLength.ShouldBe(0);
    }

    [Test]
    public void TryAppend()
    {
        var csb = new CharSpanBuilder(5);
        csb.TryAppend('a', 0).ShouldBeTrue();
        csb.TryAppend('a', 6).ShouldBeFalse();
        csb.Length.ShouldBe(0);

        csb.TryAppend('a', 6).ShouldBeFalse();
        csb.Length.ShouldBe(0);

        csb.TryAppend('a', 4).ShouldBeTrue();
        csb.Length.ShouldBe(4);
        csb.TryAppend('b', 2).ShouldBeFalse();
        csb.Length.ShouldBe(4);

        csb.TryAppend('b', 1).ShouldBeTrue();
        csb.ToString().ShouldBe("aaaab");
        csb.Length.ShouldBe(5);

        csb.TryAppend('c', 1).ShouldBeFalse();
        csb.Length.ShouldBe(5);
    }
}
