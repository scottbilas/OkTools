using System.Collections;

class EnumerableExtensionsTests
{
    [Test]
    public void OrEmpty_WithNonNullInput_ReturnsInput()
    {
        var enumerable = Array.Empty<string>();

        enumerable.OrEmpty().ShouldBe(enumerable);
    }

    [Test]
    public void OrEmpty_WithNullInput_ReturnsEmpty()
    {
        IEnumerable<string>? enumerable = null;
        enumerable.OrEmpty().ShouldBeEmpty();
    }

    [Test]
    public void SingleOr_WithEmpty_ReturnsDefault()
    {
        Array.Empty<int>().SingleOr(2).ShouldBe(2);
        Array.Empty<int>().SingleOr(() => 3).ShouldBe(3);
    }

    [Test]
    public void SingleOr_WithSingle_ReturnsElement()
    {
        new[] { 1 }.SingleOr(2).ShouldBe(1);
        new[] { 1 }.SingleOr(() => 3).ShouldBe(1);
    }

    [Test]
    public void SingleOr_WithMoreThanOne_Throws()
    {
        Should
            .Throw<InvalidOperationException>(() => new[] { 1, 2 }.SingleOr(3))
            .Message.ShouldContain("more than one");
        Should
            .Throw<InvalidOperationException>(() => new[] { 1, 2 }.SingleOr(() => 4))
            .Message.ShouldContain("more than one");
    }

    class DirectIndexTest<T>(int count, int index, T value) : IReadOnlyList<T>
    {
        public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException();
        IEnumerator IEnumerable.GetEnumerator() => throw new InvalidOperationException();

        public int Count => count;
        public T this[int i] => i == index ? value : throw new InvalidOperationException();
    }

    [Test]
    public void First_WithReadOnlyList_ShouldOnlyCallIndexer()
    {
        var list = new DirectIndexTest<int>(100, 0, 123);

        list.First().ShouldBe(123);
        Should.Throw<InvalidOperationException>(() => list.AsEnumerable().First());
    }

    [Test]
    public void Last_WithReadOnlyList_ShouldOnlyCallIndexer()
    {
        var list = new DirectIndexTest<int>(100, 99, 234);

        list.Last().ShouldBe(234);
        Should.Throw<InvalidOperationException>(() => list.AsEnumerable().Last());
    }

    [Test]
    public void TryFirst_WithNoMatch_ReturnsFalseDefault()
    {
        Array.Empty<string>().TryFirst(out var result1).ShouldBeFalse();
        result1.ShouldBe(default);

        Array.Empty<string>().TryFirst(_ => false, out var result2).ShouldBeFalse();
        result2.ShouldBe(default);
        new[] { "a", "b" }.TryFirst(_ => false, out var result3).ShouldBeFalse();
        result3.ShouldBe(default);
    }

    [Test]
    public void TryFirst_WithMatch_ReturnsTrueFound()
    {
        var arr = new[] { "a", "b" };

        arr.TryFirst(out var result1).ShouldBeTrue();
        result1.ShouldBe("a");

        arr.TryFirst(s => s[0] > 'a', out var result2).ShouldBeTrue();
        result2.ShouldBe("b");
    }

    [Test]
    public void TryLast_WithNoMatch_ReturnsFalseDefault()
    {
        Array.Empty<string>().TryLast(out var result1).ShouldBeFalse();
        result1.ShouldBe(default);

        Array.Empty<string>().TryLast(_ => false, out var result2).ShouldBeFalse();
        result2.ShouldBe(default);
        new[] { "a", "b" }.TryLast(_ => false, out var result3).ShouldBeFalse();
        result3.ShouldBe(default);
    }

    [Test]
    public void TryLast_WithMatch_ReturnsTrueFound()
    {
        var arr = new[] { "b", "a" };

        arr.TryLast(out var result1).ShouldBeTrue();
        result1.ShouldBe("a");

        arr.TryLast(s => s[0] > 'a', out var result2).ShouldBeTrue();
        result2.ShouldBe("b");
    }

    [Test]
    public void WhereNotNull_WithItemsWithNulls_ReturnsFilteredForNull()
    {
        var dummy1 = Enumerable.Empty<float>();
        var dummy2 = new InvalidOperationException();
        var enumerable = new object?[] { null, "abc", dummy1, dummy2, null, null, "ghi" };

        enumerable.WhereNotNull().ShouldBe(new object[] { "abc", dummy1, dummy2, "ghi" });
    }

    [Test]
    public void WhereNotNull_WithEmpty_ReturnsEmpty()
    {
        var enumerable = Enumerable.Empty<Exception>();

        enumerable.WhereNotNull().ShouldBeEmpty();
    }

    [Test]
    public void WhereNotNull_WithAllNulls_ReturnsEmpty()
    {
        var enumerable = new object?[] { null, null, null };

        enumerable.WhereNotNull().ShouldBeEmpty();
    }

    [Test]
    public void ToDictionary_WithTuples_ReturnsMappedDictionary()
    {
        var items = new[] { (1, "one"), (2, "two") };
        var dictionary = items.ToDictionary();

        dictionary[1].ShouldBe("one");
        dictionary[2].ShouldBe("two");
    }

    [Test]
    public void ToDictionary_TuplesWithDups_Throws()
    {
        var items = new[] { (1, "one"), (1, "two") };
        Should.Throw<Exception>(() => items.ToDictionary());
    }

    [Test]
    public void Flatten()
    {
        new[] { 1, 2, 3 }.Flatten<int>().ShouldBe(new[] { 1, 2, 3 });
        new[] { 1 }.Flatten<int>().ShouldBe(new[] { 1 });
        Array.Empty<int>().Flatten<int>().ShouldBeEmpty();

        new object[] { new[] { 1 }, 2, 3 }.Flatten<int>().ShouldBe(new[] { 1, 2, 3 });
        new object[] { new object[] { 1, new[] { 2 } }, 3 }.Flatten<int>().ShouldBe(new[] { 1, 2, 3 });
    }
}
