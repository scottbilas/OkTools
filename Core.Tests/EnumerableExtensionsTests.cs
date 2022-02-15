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

    [Test]
    public void WhereNotNull_WithItemsWithNulls_ReturnsFilteredForNull()
    {
        var dummy1 = Enumerable.Empty<float>();
        var dummy2 = new Exception();
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
}
