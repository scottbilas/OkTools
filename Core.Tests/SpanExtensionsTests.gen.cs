class ReadOnlySpanExtensionsTests
{
    [Test]
    public void SafeSlice()
    {
        ReadOnlySpan<int> span = new[] { 0, 1, 2, 3 }.AsSpan();

        span.SafeSlice(-5, 3).ToArray().ShouldBeEmpty();
        span.SafeSlice(-5, 8).ToArray().ShouldBe(new[] { 0, 1, 2 });
        span.SafeSlice(-5).ToArray().ShouldBe(new[] { 0, 1, 2, 3 });

        span.SafeSlice(2, -1).ToArray().ShouldBeEmpty();
        span.SafeSlice(2, 5).ToArray().ShouldBe(new[] { 2, 3 });
        span.SafeSlice(2).ToArray().ShouldBe(new[] { 2, 3 });

        span.SafeSlice(7, 2).ToArray().ShouldBeEmpty();
        span.SafeSlice(7, -5).ToArray().ShouldBeEmpty();
        span.SafeSlice(7).ToArray().ShouldBeEmpty();
    }
}

class SpanExtensionsTests
{
    [Test]
    public void SafeSlice()
    {
        Span<int> span = new[] { 0, 1, 2, 3 }.AsSpan();

        span.SafeSlice(-5, 3).ToArray().ShouldBeEmpty();
        span.SafeSlice(-5, 8).ToArray().ShouldBe(new[] { 0, 1, 2 });
        span.SafeSlice(-5).ToArray().ShouldBe(new[] { 0, 1, 2, 3 });

        span.SafeSlice(2, -1).ToArray().ShouldBeEmpty();
        span.SafeSlice(2, 5).ToArray().ShouldBe(new[] { 2, 3 });
        span.SafeSlice(2).ToArray().ShouldBe(new[] { 2, 3 });

        span.SafeSlice(7, 2).ToArray().ShouldBeEmpty();
        span.SafeSlice(7, -5).ToArray().ShouldBeEmpty();
        span.SafeSlice(7).ToArray().ShouldBeEmpty();
    }
}

