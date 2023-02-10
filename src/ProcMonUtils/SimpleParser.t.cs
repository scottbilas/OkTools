using OkTools.ProcMonUtils;

// ReSharper disable StringLiteralTypo

class SimpleParserTests
{
    [TestCase("", ';', 0)]
    [TestCase("babaab", 'a', 3)]
    [TestCase("abc;def", ';', 1)]
    [TestCase(";abc;def", ';', 2)]
    [TestCase(";abc;def;", ';', 3)]
    public void Count(string text, char find, int expected)
    {
        new SimpleParser(text).Count(find).ShouldBe(expected);
    }

    [TestCase("")]
    [TestCase("a")]
    [TestCase("abc")]
    [TestCase("xyzzy is the magic word!")]
    public void Expect(string str)
    {
        var parser = new SimpleParser(str);

        foreach (var c in str)
            Should.NotThrow(() => parser.Expect(c));

        Should.Throw<IndexOutOfRangeException>(() => parser.Expect('z'));
    }

    [Test]
    public void Expect_WithOffset()
    {
        const string test = "xyzzy abc";

        for (var offset = 0; offset < test.Length; ++offset)
        {
            var parser = new SimpleParser(test, offset);

            for (var i = offset; i < test.Length; ++i)
            {
                Should.NotThrow(() => parser.Expect(test[i]));
                parser.AsSpan().ToString().ShouldBe(test[(i+1)..]);
            }

            Should.Throw<IndexOutOfRangeException>(() => parser.Expect('z'));
        }
    }

    [Test]
    public void Expect_WithNonExpectedChar_Throws()
    {
        var parser = new SimpleParser("a");
        Should.Throw<SimpleParserException>(() => parser.Expect('b'));
    }

    [TestCase("", "a", typeof(IndexOutOfRangeException))]
    [TestCase("abc", "b", typeof(SimpleParserException))]
    [TestCase("abc", "abd", typeof(SimpleParserException))]
    [TestCase("abc", "abcd", typeof(IndexOutOfRangeException))]
    public void ExpectString_WithInvalidContent_Throws(string text, string expected, Type expectedException)
    {
        Should.Throw(() => new SimpleParser(text).Expect(expected), expectedException);
    }

    [TestCase("", "")]
    [TestCase("abc", "a")]
    [TestCase("abc", "ab")]
    [TestCase("abc", "abc")]
    public void ExpectString_WithValidContent(string text, string expected)
    {
        var parser = new SimpleParser(text);
        Should.NotThrow(() => parser.Expect(expected));
        parser.Offset.ShouldBe(expected.Length);
    }

    [TestCase(";", ';', "")]
    [TestCase("abc;", ';', "abc")]
    [TestCase(";abc", ';', "")]
    public void ReadStringUntil(string test, char terminator, string expected)
    {
        new SimpleParser(test).ReadStringUntil(terminator).ToString().ShouldBe(expected);
    }

    [Test]
    public void ReadStringUntil_WithNoTerminator_Throws()
    {
        Should.Throw<IndexOutOfRangeException>(() => new SimpleParser("abc").ReadStringUntil(';'));
    }

    [TestCase("")]
    [TestCase("a")]
    [TestCase("abc")]
    [TestCase("xyzzy is the magic word!")]
    public void ReadChar(string str)
    {
        var parser = new SimpleParser(str);

        foreach (var c in str)
            parser.ReadChar().ShouldBe(c);

        Should.Throw<IndexOutOfRangeException>(() => parser.ReadChar());
    }

    [TestCase("", typeof(IndexOutOfRangeException))]
    [TestCase("a", typeof(SimpleParserException))]
    [TestCase(";123", typeof(SimpleParserException))]
    [TestCase("18446744073709551616", typeof(OverflowException))]
    [TestCase("-1", typeof(SimpleParserException))]
    public void ReadULong_WithInvalidContent_Throws(string text, Type expectedException)
    {
        Should.Throw(() => new SimpleParser(text).ReadULong(), expectedException);
    }

    [TestCase("0", 0u)]
    [TestCase("1", 1u)]
    [TestCase("123", 123u)]
    [TestCase("18446744073709551615", 18446744073709551615)]
    public void ReadULong_WithValidContent(string text, ulong expected)
    {
        new SimpleParser(text).ReadULong().ShouldBe(expected);
    }

    [TestCase("", typeof(SimpleParserException))]
    [TestCase("g", typeof(SimpleParserException))]
    [TestCase(";123", typeof(SimpleParserException))]
    [TestCase("10000000000000000", typeof(OverflowException))]
    [TestCase("-1", typeof(SimpleParserException))]
    public void ReadULongHex_WithInvalidContent_Throws(string text, Type expectedException)
    {
        Should.Throw(() => new SimpleParser(text).ReadULongHex(), expectedException);
    }

    [TestCase("0", 0u)]
    [TestCase("1", 1u)]
    [TestCase("f", 15u)]
    [TestCase("dead123BEEF", 0xdead123BEEFul)]
    [TestCase("FFFFFFFFFFFFFFFF", 0xFFFFFFFFFFFFFFFFul)]
    public void ReadULongHex_WithValidContent_Returns(string text, ulong expected)
    {
        new SimpleParser(text).ReadULongHex().ShouldBe(expected);
    }

    [Test]
    public void Basics()
    {
        const string text = "123;xyzzy,0xDEADf00d00d00 ? 7734";

        var parser = new SimpleParser(text);
        parser.Remain.ShouldBe(text.Length);
        parser.AtEnd.ShouldBeFalse();

        parser.ReadULong().ShouldBe(123u);
        parser.Expect(';');

        parser.ReadStringUntil(',').ToString().ShouldBe("xyzzy");
        parser.Expect(',');

        parser.Expect("0x");
        parser.ReadULongHex().ShouldBe(0xdeadf00d00d00ul);
        parser.Expect(' ');

        parser.ReadChar().ShouldBe('?');
        parser.Expect(' ');
        parser.Remain.ShouldBe(4);
        parser.AtEnd.ShouldBeFalse();

        parser.ReadULong().ShouldBe(7734ul);
        parser.Remain.ShouldBe(0);
        parser.AtEnd.ShouldBeTrue();
    }
}
