using System.Globalization;

class TimeSpanExtensionsTests
{
    CultureInfo? _lastCulture;

    [SetUp]
    public void SetUp()
    {
        // culture stuff required to avoid test instability on dot vs comma
        _lastCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    [TearDown]
    public void TearDown()
    {
        CultureInfo.CurrentCulture = _lastCulture!;
    }

    [TestCase(true, " ago")]
    [TestCase(false, "")]
    public void ToNiceAge_Basics(bool ago, string extraText)
    {
        new TimeSpan(20000, 5, 8, 11, 123).ToNiceAge(ago).ShouldBe("54.8yr" + extraText);
        new TimeSpan(1000, 5, 8, 11, 123).ToNiceAge(ago).ShouldBe("2.7yr" + extraText);
        new TimeSpan(500, 5, 8, 11, 123).ToNiceAge(ago).ShouldBe("16.7mo" + extraText);
        new TimeSpan(100, 5, 8, 11, 123).ToNiceAge(ago).ShouldBe("3.3mo" + extraText);
        new TimeSpan(50, 5, 8, 11, 123).ToNiceAge(ago).ShouldBe("7.2wk" + extraText);
        new TimeSpan(10, 5, 8, 11, 123).ToNiceAge(ago).ShouldBe("10d5h" + extraText);
        new TimeSpan(1, 5, 8, 11, 123).ToNiceAge(ago).ShouldBe("1d5h" + extraText);
        new TimeSpan(0, 5, 8, 11, 123).ToNiceAge(ago).ShouldBe("5h8m" + extraText);
        new TimeSpan(0, 0, 8, 11, 123).ToNiceAge(ago).ShouldBe("8m11s" + extraText);
        new TimeSpan(0, 0, 0, 11, 123).ToNiceAge(ago).ShouldBe("11.1s" + extraText);
        new TimeSpan(0, 0, 0, 1, 123).ToNiceAge(ago).ShouldBe("1.12s" + extraText);
        new TimeSpan(0, 0, 0, 0, 123).ToNiceAge(ago).ShouldBe("0.12s" + extraText);
    }

    [Test]
    public void ToNiceAge_Now()
    {
        default(TimeSpan).ToNiceAge(true).ShouldBe("now");
        // ReSharper disable once RedundantArgumentDefaultValue
        default(TimeSpan).ToNiceAge(false).ShouldBe("now");
    }
}
