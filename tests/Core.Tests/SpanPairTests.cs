class SpanPairTests
{
    // keep all of these as functions and not static initializers
    static int[] EmptyArray => Array.Empty<int>();
    static int[] FirstArray => new[] { 1, 2 };
    static int[] SecondArray => new[] { 3, 4, 5 };
    static int[] BothArray => FirstArray.Concat(SecondArray).ToArray();
    static SpanPair<int> EmptyPair => default;
    static SpanPair<int> FirstPair => new(FirstArray);
    static SpanPair<int> SecondPair => new(default, SecondArray);
    static SpanPair<int> BothPair => new(FirstArray, SecondArray);

    [Test]
    public void Ctor_WithBothSpans()
    {
        var both = BothPair;
        both.Span0.Length.ShouldBe(2);
        both.Span0.ToArray().ShouldBe(FirstArray);
        both.Span1.Length.ShouldBe(3);
        both.Span1.ToArray().ShouldBe(SecondArray);

        both.Length.ShouldBe(5);
        both.Any.ShouldBeTrue();
        both.IsEmpty.ShouldBeFalse();
    }

    [Test]
    public void Ctor_WithDefault()
    {
        var empty = EmptyPair;
        empty.Span0.Length.ShouldBe(0);
        empty.Span1.Length.ShouldBe(0);

        empty.Length.ShouldBe(0);
        empty.Any.ShouldBeFalse();
        empty.IsEmpty.ShouldBeTrue();
    }

    [Test]
    public void Ctor_WithNoSpan1Arg()
    {
        var first = FirstPair;
        first.Span0.Length.ShouldBe(2);
        first.Span0.ToArray().ShouldBe(FirstArray);
        first.Span1.Length.ShouldBe(0);

        first.Length.ShouldBe(2);
        first.Any.ShouldBeTrue();
        first.IsEmpty.ShouldBeFalse();
    }

    [Test]
    public void Indexer()
    {
        var both = BothPair;
        for (var i = 0; i < both.Length; ++i)
            both[i].ShouldBe(BothArray[i]);

        var first = FirstPair;
        for (var i = 0; i < first.Length; ++i)
            first[i].ShouldBe(FirstArray[i]);

        var second = SecondPair;
        for (var i = 0; i < second.Length; ++i)
            second[i].ShouldBe(SecondArray[i]);
    }

    [Test]
    public void Indexer_WithOutOfRange_Throws()
    {
        Should.Throw<IndexOutOfRangeException>(() => { var _ = EmptyPair[-1]; });
        Should.Throw<IndexOutOfRangeException>(() => { var _ = EmptyPair[0]; });

        Should.Throw<IndexOutOfRangeException>(() => { var _ = BothPair[-1]; });
        Should.Throw<IndexOutOfRangeException>(() => { var _ = BothPair[BothArray.Length]; });

        Should.Throw<IndexOutOfRangeException>(() => { var _ = FirstPair[-1]; });
        Should.Throw<IndexOutOfRangeException>(() => { var _ = FirstPair[FirstArray.Length]; });

        Should.Throw<IndexOutOfRangeException>(() => { var _ = SecondPair[-1]; });
        Should.Throw<IndexOutOfRangeException>(() => { var _ = SecondPair[SecondArray.Length]; });
    }

    [Test]
    public void EqualityOperators()
    {
        static void Test<T>(SpanPair<T> test0, SpanPair<T> test1)
        {
            #pragma warning disable CS1718
            // ReSharper disable EqualExpressionComparison

            (test0 == test0).ShouldBeTrue();
            (test0 == test1).ShouldBeFalse();

            (test0 != test0).ShouldBeFalse();
            (test0 != test1).ShouldBeTrue();

            var test2 = new SpanPair<T>(test0.Span0, test0.Span1);
            (test2 == test0).ShouldBeTrue();
            (test2 == test1).ShouldBeFalse();

            // ReSharper restore EqualExpressionComparison
            #pragma warning restore CS1718
        }

        Test(BothPair, BothPair);
        Test(FirstPair, FirstPair);
        Test(SecondPair, SecondPair);
    }

    [Test]
    public void EqualityOperators_WithEmpty()
    {
        var test0 = EmptyPair;
        var test1 = EmptyPair;

        #pragma warning disable CS1718
        // ReSharper disable EqualExpressionComparison

        (test0 == test0).ShouldBeTrue();
        (test0 == test1).ShouldBeTrue();

        (test0 != test0).ShouldBeFalse();
        (test0 != test1).ShouldBeFalse();

        var test2 = new SpanPair<int>(test0.Span0, test0.Span1);
        (test2 == test0).ShouldBeTrue();
        (test2 == test1).ShouldBeTrue();

        // ReSharper restore EqualExpressionComparison
        #pragma warning restore CS1718
    }

    [Test]
    public void UnsupportedMethods_Throw()
    {
        // ReSharper disable once SuspiciousTypeConversion.Global
        Should.Throw<InvalidOperationException>(() => BothPair.Equals("foo"));

        Should.Throw<InvalidOperationException>(() => BothPair.GetHashCode());
    }

    [Test]
    public void ToString_DoesNotThrow()
    {
        // just ensure we get something back (don't care what Span.ToString produces)

        EmptyPair.ToString().ShouldNotBeNullOrWhiteSpace();
        BothPair.ToString().ShouldNotBeNullOrWhiteSpace();
        FirstPair.ToString().ShouldNotBeNullOrWhiteSpace();
        SecondPair.ToString().ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public void Clear()
    {
        static void Test<T>(SpanPair<T> pair)
        {
            pair.Clear();

            foreach (var i in pair.Span0)
                i.ShouldBe(default);
            foreach (var i in pair.Span1)
                i.ShouldBe(default);
            foreach (var i in pair)
                i.ShouldBe(default);
        }

        Test(EmptyPair);
        Test(BothPair);
        Test(FirstPair);
        Test(SecondPair);
    }

    [Test]
    public void Fill()
    {
        static void Test<T>(SpanPair<T> pair, T value)
        {
            pair.Fill(value);

            foreach (var i in pair.Span0)
                i.ShouldBe(value);
            foreach (var i in pair.Span1)
                i.ShouldBe(value);
            foreach (var i in pair)
                i.ShouldBe(value);
        }

        Test(EmptyPair, 6);
        Test(BothPair, 7);
        Test(FirstPair, 8);
        Test(SecondPair, 9);
    }

    [Test]
    public void Slice_WithOutOfRange_Throws()
    {
        static void Test<T>(T[] first, T[] second)
        {
            var len = first.Length + second.Length;

            Should.Throw<ArgumentOutOfRangeException>(() => new SpanPair<T>(first, second).Slice(-1));
            Should.Throw<ArgumentOutOfRangeException>(() => new SpanPair<T>(first, second).Slice(-1));
            Should.Throw<ArgumentOutOfRangeException>(() => new SpanPair<T>(first, second).Slice(len+1));

            Should.Throw<ArgumentOutOfRangeException>(() => new SpanPair<T>(first, second).Slice(-1, -1));
            Should.Throw<ArgumentOutOfRangeException>(() => new SpanPair<T>(first, second).Slice(-1, len));
            Should.Throw<ArgumentOutOfRangeException>(() => new SpanPair<T>(first, second).Slice(-1, len+1));

            Should.Throw<ArgumentOutOfRangeException>(() => new SpanPair<T>(first, second).Slice(0, -1));
            Should.Throw<ArgumentOutOfRangeException>(() => new SpanPair<T>(first, second).Slice(0, len+1));

            Should.Throw<ArgumentOutOfRangeException>(() => new SpanPair<T>(first, second).Slice(len, -1));
            Should.Throw<ArgumentOutOfRangeException>(() => new SpanPair<T>(first, second).Slice(len, 1));
        }

        Test(EmptyArray, EmptyArray);
        Test(FirstArray, SecondArray);
        Test(FirstArray, EmptyArray);
        Test(EmptyArray, SecondArray);
    }

    [Test]
    public void Slice_WithEmpty()
    {
        (EmptyPair.Slice(0) == default).ShouldBeTrue();
        (EmptyPair.Slice(0, 0) == default).ShouldBeTrue();
    }

    [Test]
    public void Slice()
    {
        static void Test<T>(SpanPair<T> pair, T[] array)
        {
            pair[..].ToArray().ShouldBe(array[..]);

            for (var begin = 0; begin != array.Length; ++begin)
                for (var end = begin; end != array.Length; ++end)
                    pair[begin..end].ToArray().ShouldBe(array[begin..end]);
        }

        Test(EmptyPair, EmptyArray);
        Test(BothPair, BothArray);
        Test(FirstPair, FirstArray);
        Test(SecondPair, SecondArray);
    }

    [Test]
    public void ToArray()
    {
        EmptyPair.ToArray().ShouldBeEmpty();
        BothPair.ToArray().ShouldBe(BothArray);
        FirstPair.ToArray().ShouldBe(FirstArray);
        SecondPair.ToArray().ShouldBe(SecondArray);
    }
}
