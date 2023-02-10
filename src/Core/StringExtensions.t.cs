// ReSharper disable StringLiteralTypo

class StringExtensionsTests
{
    [Test]
    public void IndexOfNot()
    {
        "aaabaa".IndexOfNot('a').ShouldBe(3);
        "aaabaa".IndexOfNot('a', 0).ShouldBe(3);
        "aaabaa".IndexOfNot('a', 0, 0).ShouldBe(-1);
        "aaabaa".IndexOfNot('a', 1).ShouldBe(3);
        "aaabaa".IndexOfNot('a', 1, 0).ShouldBe(-1);
        "aaabaa".IndexOfNot('a', 1, 1).ShouldBe(-1);
        "aaabaa".IndexOfNot('a', 1, 2).ShouldBe(-1);
        "aaabaa".IndexOfNot('a', 1, 3).ShouldBe(3);
        "aaabaa".IndexOfNot('a', 2).ShouldBe(3);
        "aaabaa".IndexOfNot('a', 3).ShouldBe(3);
        "aaabaa".IndexOfNot('a', 4).ShouldBe(-1);

        "aAabaa".IndexOfNot('a').ShouldBe(1);
        "aaabaa".IndexOfNot('b').ShouldBe(0);
        "".IndexOfNot('a').ShouldBe(-1);
    }

    [Test]
    public void IndexOfNot_OutOfBounds_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => "aaab".IndexOfNot('a', -10));
        Should.Throw<ArgumentOutOfRangeException>(() => "aaab".IndexOfNot('a', 5));
        Should.Throw<ArgumentOutOfRangeException>(() => "aaab".IndexOfNot('a', 2, 10));
        Should.Throw<ArgumentOutOfRangeException>(() => "aaab".IndexOfNot('a', 2, 3));
    }

    [Test]
    public void LastIndexOfNot()
    {
        "aaabaa".LastIndexOfNot('a').ShouldBe(3);
        "aaabaa".LastIndexOfNot('a', 0).ShouldBe(3);
        "aaabaa".LastIndexOfNot('a', 0, 0).ShouldBe(-1);
        "aaabaa".LastIndexOfNot('a', 1).ShouldBe(3);
        "aaabaa".LastIndexOfNot('a', 1, 0).ShouldBe(-1);
        "aaabaa".LastIndexOfNot('a', 1, 1).ShouldBe(-1);
        "aaabaa".LastIndexOfNot('a', 1, 2).ShouldBe(-1);
        "aaabaa".LastIndexOfNot('a', 1, 3).ShouldBe(3);
        "aaabaa".LastIndexOfNot('a', 2).ShouldBe(3);
        "aaabaa".LastIndexOfNot('a', 3).ShouldBe(3);
        "aaabaa".LastIndexOfNot('a', 4).ShouldBe(-1);

        "aAabaa".LastIndexOfNot('a').ShouldBe(3);
        "aaabaa".LastIndexOfNot('b').ShouldBe(5);
        "".LastIndexOfNot('a').ShouldBe(-1);
    }

    [Test]
    public void LastIndexOfNot_OutOfBounds_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => "baaa".LastIndexOfNot('a', -10));
        Should.Throw<ArgumentOutOfRangeException>(() => "baaa".LastIndexOfNot('a', 5));
        Should.Throw<ArgumentOutOfRangeException>(() => "baaa".LastIndexOfNot('a', 2, 10));
        Should.Throw<ArgumentOutOfRangeException>(() => "baaa".LastIndexOfNot('a', 2, 3));
    }

    [Test]
    public void Left_InBounds_ReturnsSubstring()
    {
        "".Left(0).ShouldBe("");
        "abc".Left(2).ShouldBe("ab");
        "abc".Left(0).ShouldBe("");
    }

    [Test]
    public void Left_OutOfBounds_ClampsProperly()
    {
        "".Left(10).ShouldBe("");
        "abc".Left(10).ShouldBe("abc");
    }

    [Test]
    public void Left_BadInput_Throws()
    {
        Should.Throw<Exception>(() => ((string?)null)!.Left(1));
        Should.Throw<Exception>(() => "abc".Left(-1));
    }

    [Test]
    public void Mid_InBounds_ReturnsSubstring()
    {
        "".Mid(0, 0).ShouldBe("");
        "abc".Mid(0, 3).ShouldBe("abc");
        "abc".Mid(0).ShouldBe("abc");
        "abc".Mid(0, -2).ShouldBe("abc");
        "abc".Mid(1, 1).ShouldBe("b");
        "abc".Mid(3, 0).ShouldBe("");
        "abc".Mid(0, 0).ShouldBe("");
    }

    [Test]
    public void Mid_OutOfBounds_ClampsProperly()
    {
        "".Mid(10, 5).ShouldBe("");
        "abc".Mid(0, 10).ShouldBe("abc");
        "abc".Mid(1, 10).ShouldBe("bc");
        "abc".Mid(10, 5).ShouldBe("");
    }

    [Test]
    public void Mid_BadInput_Throws()
    {
        Should.Throw<Exception>(() => ((string)null!).Mid(1, 2));
        Should.Throw<Exception>(() => "abc".Mid(-1));
    }

    [Test]
    public void Right_InBounds_ReturnsSubstring()
    {
        "".Right(0).ShouldBe("");
        "abc".Right(2).ShouldBe("bc");
        "abc".Right(0).ShouldBe("");
    }

    [Test]
    public void Right_OutOfBounds_ClampsProperly()
    {
        "".Right(10).ShouldBe("");
        "abc".Right(10).ShouldBe("abc");
    }

    [Test]
    public void Right_BadInput_Throws()
    {
        Should.Throw<Exception>(() => ((string)null!).Right(1));
        Should.Throw<Exception>(() => "abc".Right(-1));
    }

    [Test]
    public void Truncate_ThatDoesNotShorten_ReturnsSameInstance()
    {
        const string text = "abc def";
        ReferenceEquals(text, text.Truncate(100)).ShouldBeTrue();
        ReferenceEquals(text, text.Truncate(10)).ShouldBeTrue();
        ReferenceEquals(text, text.Truncate(text.Length)).ShouldBeTrue();
    }

    [Test]
    public void Truncate_ThatShortens_TruncatesAndAddsTrailer()
    {
        "abc def".Truncate(6).ShouldBe("abc...");
        "abc def".Truncate(5).ShouldBe("ab...");
        "abc def".Truncate(4).ShouldBe("a...");
        "abc def".Truncate(3).ShouldBe("...");

        "abc def".Truncate(6, "ghi").ShouldBe("abcghi");
        "abc def".Truncate(5, "ghi").ShouldBe("abghi");
        "abc def".Truncate(4, "ghi").ShouldBe("aghi");
        "abc def".Truncate(3, "ghi").ShouldBe("ghi");
    }

    [Test]
    public void Truncate_WithTooBigTrailer_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => "abc def".Truncate(2));
        Should.Throw<ArgumentException>(() => "abc def".Truncate(5, "123456"));
    }

    [Test]
    public void Truncate_WithUnderflow_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => "abc def".Truncate(-2));
        Should.Throw<ArgumentException>(() => "abc def".Truncate(0, "ghi"));
    }

    [Test]
    public void StringJoin_WithEmpty_ReturnsEmptyString()
    {
        var enumerable = Array.Empty<object>();

        enumerable.StringJoin(", ").ShouldBe("");
        enumerable.StringJoin(';').ShouldBe("");
    }

    [Test]
    public void StringJoin_WithSingle_ReturnsNoSeparators()
    {
        var enumerable = new[] { "abc" };

        enumerable.StringJoin(", ").ShouldBe("abc");
        enumerable.StringJoin(';').ShouldBe("abc");
    }

    [Test]
    public void StringJoin_WithMultiple_ReturnsJoined()
    {
        var enumerable = new object[] { "abc", 0b111001, -14, 'z' };

        enumerable.StringJoin(" ==> ").ShouldBe("abc ==> 57 ==> -14 ==> z");
        enumerable.StringJoin('\n').ShouldBe("abc\n57\n-14\nz");
    }

    [Test]
    public void ExpandTabs_WithEmpty_Returns_Empty()
    {
        "".ExpandTabs(4).ShouldBeEmpty();
    }

    [Test]
    public void ExpandTabs_WithNoTabs_ReturnsSameInstance() // i.e. no allocs
    {
        const string text = "abc def ghijkl";
        ReferenceEquals(text, text.ExpandTabs(4)).ShouldBeTrue();
    }

    [Test]
    public void ExpandTabs_WithInvalidTabWidth_Throws()
    {
        Should.Throw<ArgumentException>(() => "".ExpandTabs(-123));
        Should.Throw<ArgumentException>(() => "abc".ExpandTabs(-1));
    }

    [Test]
    public void ExpandTabs_BasicScenarios_ExpandsProperly()
    {
        "a\tbc\t\td".ExpandTabs(4).ShouldBe("a   bc      d");
        "a\tbc\t\td".ExpandTabs(3).ShouldBe("a  bc    d");
        "a\tbc\t\td".ExpandTabs(2).ShouldBe("a bc    d");
        "a\tbc\t\td".ExpandTabs(1).ShouldBe("a bc  d");
        "a\tbc\t\td".ExpandTabs(0).ShouldBe("abcd");
    }

    [Test]
    public void ExpandTabs_WithUnnecessaryBuffer_DoesNotUseBuffer()
    {
        var buffer = new StringBuilder { Capacity = 0 };

        var expandedA = "\ta".ExpandTabs(0, buffer);
        expandedA.ShouldBe("a");
        buffer.Capacity.ShouldBe(0);

        var expandedB = "\tb".ExpandTabs(1, buffer);
        expandedB.ShouldBe(" b");
        buffer.Capacity.ShouldBe(0);

        var expandedC = "\tc".ExpandTabs(2, buffer);
        expandedC.ShouldBe("  c");
        buffer.Capacity.ShouldNotBe(0);
    }

    [Test]
    public void ExpandTabs_WithReusedBuffer_DoesNotReusePreviousResults()
    {
        // this is a bugfix test. note tab width of 2 to avoid early-out that doesn't use the string builder.

        var buffer = new StringBuilder();
        "\ta".ExpandTabs(2, buffer).ShouldBe("  a");
        buffer.Length.ShouldBe(0); // no leftovers
        "\tb".ExpandTabs(2, buffer).ShouldBe("  b"); // not "  a b"
        buffer.Length.ShouldBe(0);
    }
}
