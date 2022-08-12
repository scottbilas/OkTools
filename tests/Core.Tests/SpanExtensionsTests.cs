class ReadOnlySpanExtensionsTests
{
    [Test]
    public void SliceSafe()
    {
        ReadOnlySpan<int> span = new[] { 0, 1, 2, 3 }.AsSpan();

        span.SliceSafe(-5, 3).ToArray().ShouldBeEmpty();
        span.SliceSafe(-5, 8).ToArray().ShouldBe(new[] { 0, 1, 2 });
        span.SliceSafe(-5).ToArray().ShouldBe(new[] { 0, 1, 2, 3 });

        span.SliceSafe(2, -1).ToArray().ShouldBeEmpty();
        span.SliceSafe(2, 5).ToArray().ShouldBe(new[] { 2, 3 });
        span.SliceSafe(2).ToArray().ShouldBe(new[] { 2, 3 });

        span.SliceSafe(7, 2).ToArray().ShouldBeEmpty();
        span.SliceSafe(7, -5).ToArray().ShouldBeEmpty();
        span.SliceSafe(7).ToArray().ShouldBeEmpty();
    }
}

class SpanExtensionsTests
{
    [Test]
    public void SliceSafe()
    {
        Span<int> span = new[] { 0, 1, 2, 3 }.AsSpan();

        span.SliceSafe(-5, 3).ToArray().ShouldBeEmpty();
        span.SliceSafe(-5, 8).ToArray().ShouldBe(new[] { 0, 1, 2 });
        span.SliceSafe(-5).ToArray().ShouldBe(new[] { 0, 1, 2, 3 });

        span.SliceSafe(2, -1).ToArray().ShouldBeEmpty();
        span.SliceSafe(2, 5).ToArray().ShouldBe(new[] { 2, 3 });
        span.SliceSafe(2).ToArray().ShouldBe(new[] { 2, 3 });

        span.SliceSafe(7, 2).ToArray().ShouldBeEmpty();
        span.SliceSafe(7, -5).ToArray().ShouldBeEmpty();
        span.SliceSafe(7).ToArray().ShouldBeEmpty();
    }
}
