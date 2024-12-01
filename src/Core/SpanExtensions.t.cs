// DO NOT MODIFY, THIS FILE IS GENERATED

class ReadOnlySpanExtensionTests
{
    [Test]
    public void SliceSafe()
    {
        ReadOnlySpan<int> span = new[] { 0, 1, 2, 3 }.AsSpan();

        span.SliceSafe(-5, 3).ToArray().ShouldBeEmpty();
        span.SliceSafe(-5, 8).ToArray().ShouldBe([0, 1, 2]);
        span.SliceSafe(-5).ToArray().ShouldBe([0, 1, 2, 3]);

        span.SliceSafe(2, -1).ToArray().ShouldBeEmpty();
        span.SliceSafe(2, 5).ToArray().ShouldBe([2, 3]);
        span.SliceSafe(2).ToArray().ShouldBe([2, 3]);

        span.SliceSafe(7, 2).ToArray().ShouldBeEmpty();
        span.SliceSafe(7, -5).ToArray().ShouldBeEmpty();
        span.SliceSafe(7).ToArray().ShouldBeEmpty();
    }

    [Test]
    public void SliceSafe_WithEmpty()
    {
        ReadOnlySpan<int> span = new int[0].AsSpan();

        span.SliceSafe(-5, 3).ToArray().ShouldBeEmpty();
        span.SliceSafe(-5, 8).ToArray().ShouldBeEmpty();
        span.SliceSafe(-5).ToArray().ShouldBeEmpty();

        span.SliceSafe(2, -1).ToArray().ShouldBeEmpty();
        span.SliceSafe(2, 5).ToArray().ShouldBeEmpty();
        span.SliceSafe(2).ToArray().ShouldBeEmpty();

        span.SliceSafe(7, 2).ToArray().ShouldBeEmpty();
        span.SliceSafe(7, -5).ToArray().ShouldBeEmpty();
        span.SliceSafe(7).ToArray().ShouldBeEmpty();
    }
}

class SpanExtensionTests
{
    [Test]
    public void SliceSafe()
    {
        Span<int> span = new[] { 0, 1, 2, 3 }.AsSpan();

        span.SliceSafe(-5, 3).ToArray().ShouldBeEmpty();
        span.SliceSafe(-5, 8).ToArray().ShouldBe([0, 1, 2]);
        span.SliceSafe(-5).ToArray().ShouldBe([0, 1, 2, 3]);

        span.SliceSafe(2, -1).ToArray().ShouldBeEmpty();
        span.SliceSafe(2, 5).ToArray().ShouldBe([2, 3]);
        span.SliceSafe(2).ToArray().ShouldBe([2, 3]);

        span.SliceSafe(7, 2).ToArray().ShouldBeEmpty();
        span.SliceSafe(7, -5).ToArray().ShouldBeEmpty();
        span.SliceSafe(7).ToArray().ShouldBeEmpty();
    }

    [Test]
    public void SliceSafe_WithEmpty()
    {
        Span<int> span = new int[0].AsSpan();

        span.SliceSafe(-5, 3).ToArray().ShouldBeEmpty();
        span.SliceSafe(-5, 8).ToArray().ShouldBeEmpty();
        span.SliceSafe(-5).ToArray().ShouldBeEmpty();

        span.SliceSafe(2, -1).ToArray().ShouldBeEmpty();
        span.SliceSafe(2, 5).ToArray().ShouldBeEmpty();
        span.SliceSafe(2).ToArray().ShouldBeEmpty();

        span.SliceSafe(7, 2).ToArray().ShouldBeEmpty();
        span.SliceSafe(7, -5).ToArray().ShouldBeEmpty();
        span.SliceSafe(7).ToArray().ShouldBeEmpty();
    }
}
