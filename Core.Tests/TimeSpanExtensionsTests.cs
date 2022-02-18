using System.Globalization;

class TimeSpanExtensionsTests
{
    CultureInfo? _lastCulture;

    [SetUp]
    public void SetUp()
    {
        _lastCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    [TearDown]
    public void TearDown()
    {
        CultureInfo.CurrentCulture = _lastCulture!;
    }

    [Test]
    public void ToNiceAge()
    {
        new TimeSpan(20000, 5, 8, 11, 123).ToNiceAge().ShouldBe("54.8yr");
        new TimeSpan(1000, 5, 8, 11, 123).ToNiceAge().ShouldBe("2.7yr");
        new TimeSpan(500, 5, 8, 11, 123).ToNiceAge().ShouldBe("16.7mo");
        new TimeSpan(100, 5, 8, 11, 123).ToNiceAge().ShouldBe("3.3mo");
        new TimeSpan(50, 5, 8, 11, 123).ToNiceAge().ShouldBe("7.2wk");
        new TimeSpan(10, 5, 8, 11, 123).ToNiceAge().ShouldBe("10d5h");
        new TimeSpan(1, 5, 8, 11, 123).ToNiceAge().ShouldBe("1d5h");
        new TimeSpan(0, 5, 8, 11, 123).ToNiceAge().ShouldBe("5h8m");
        new TimeSpan(0, 0, 8, 11, 123).ToNiceAge().ShouldBe("8m11s");
        new TimeSpan(0, 0, 0, 11, 123).ToNiceAge().ShouldBe("11.1s");
        new TimeSpan(0, 0, 0, 1, 123).ToNiceAge().ShouldBe("1.12s");
        new TimeSpan(0, 0, 0, 0, 123).ToNiceAge().ShouldBe("0.12s");
        default(TimeSpan).ToNiceAge().ShouldBe("now");
    }
}
