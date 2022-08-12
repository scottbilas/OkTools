using System.Collections;

class ArrayExtensionsTests
{
    [Test]
    public void ShiftLeftRight()
    {
        var array = new[] { 1, 2, 3, 4, 5 };

        // no fill

        array.ShiftLeft(2);
        array.ShouldBe(new[] { 3, 4, 5, 4, 5 });
        array.ShiftLeft(0);
        array.ShouldBe(new[] { 3, 4, 5, 4, 5 });

        array.ShiftRight(3);
        array.ShouldBe(new[] { 3, 4, 5, 3, 4 });
        array.ShiftRight(0);
        array.ShouldBe(new[] { 3, 4, 5, 3, 4 });

        // fill

        array.ShiftLeft(1, 10);
        array.ShouldBe(new[] { 4, 5, 3, 4, 10 });
        array.ShiftLeft(0, 10);
        array.ShouldBe(new[] { 4, 5, 3, 4, 10 });

        array.ShiftRight(2, 20);
        array.ShouldBe(new[] { 20, 20, 4, 5, 3 });
        array.ShiftRight(0, 10);
        array.ShouldBe(new[] { 20, 20, 4, 5, 3 });
    }

    [Test]
    public void BitArray_ShiftLeftRightFixed()
    {
        var array = new BitArray(new[] { false, true, true, false, true });

        array.ShiftLeftFixed(2);
        array.ShouldBe(new BitArray(new[] { true, false, true, false, false }));
        array.ShiftLeftFixed(0);
        array.ShouldBe(new BitArray(new[] { true, false, true, false, false }));

        array.ShiftRightFixed(3);
        array.ShouldBe(new BitArray(new[] { false, false, false, true, false }));
        array.ShiftRightFixed(0);
        array.ShouldBe(new BitArray(new[] { false, false, false, true, false }));
    }

    [Test]
    public void Shift()
    {
        var array = new[] { 1, 2, 3, 4, 5 };

        // no fill

        array.Shift(-2);
        array.ShouldBe(new[] { 3, 4, 5, 4, 5 });
        array.Shift(0);
        array.ShouldBe(new[] { 3, 4, 5, 4, 5 });

        array.Shift(3);
        array.ShouldBe(new[] { 3, 4, 5, 3, 4 });
        array.Shift(0);
        array.ShouldBe(new[] { 3, 4, 5, 3, 4 });

        // fill

        array.Shift(-1, 10);
        array.ShouldBe(new[] { 4, 5, 3, 4, 10 });
        array.Shift(0, 10);
        array.ShouldBe(new[] { 4, 5, 3, 4, 10 });

        array.Shift(2, 20);
        array.ShouldBe(new[] { 20, 20, 4, 5, 3 });
        array.Shift(0, 10);
        array.ShouldBe(new[] { 20, 20, 4, 5, 3 });
    }

    [Test]
    public void BitArray_Shift()
    {
        var array = new BitArray(new[] { false, true, true, false, true });

        array.Shift(-2);
        array.ShouldBe(new BitArray(new [] { true, false, true, false, false }));
        array.Shift(0);
        array.ShouldBe(new BitArray(new [] { true, false, true, false, false }));

        array.Shift(3);
        array.ShouldBe(new BitArray(new[] { false, false, false, true, false }));
        array.Shift(0);
        array.ShouldBe(new BitArray(new[] { false, false, false, true, false }));
    }

    [Test]
    public void ShiftLeft_WithInvalidCount_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.ShiftLeft(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.ShiftLeft(-1, 100));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.ShiftLeft(-10));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.ShiftLeft(-10, 100));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.ShiftLeft(2));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.ShiftLeft(2, 100));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.ShiftLeft(10));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.ShiftLeft(10, 100));

        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.ShiftLeft(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.ShiftLeft(-1, 100));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.ShiftLeft(-10));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.ShiftLeft(-10, 100));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.ShiftLeft(5));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.ShiftLeft(5, 100));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.ShiftLeft(10));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.ShiftLeft(10, 100));
    }

    [Test]
    public void BitArray_ShiftLeftFixed_WithInvalidCount_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true }).ShiftLeftFixed(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true }).ShiftLeftFixed(-10));
        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true }).ShiftLeftFixed(2));
        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true }).ShiftLeftFixed(10));

        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true, false, false, true }).ShiftLeftFixed(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true, false, false, true }).ShiftLeftFixed(-10));
        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true, false, false, true }).ShiftLeftFixed(5));
        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true, false, false, true }).ShiftLeftFixed(10));
    }

    [Test]
    public void ShiftRight_WithInvalidCount_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.ShiftRight(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.ShiftRight(-1, 100));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.ShiftRight(-10));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.ShiftRight(-10, 100));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.ShiftRight(2));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.ShiftRight(2, 100));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.ShiftRight(10));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.ShiftRight(10, 100));

        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.ShiftRight(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.ShiftRight(-1, 100));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.ShiftRight(-10));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.ShiftRight(-10, 100));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.ShiftRight(5));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.ShiftRight(5, 100));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.ShiftRight(10));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.ShiftRight(10, 100));
    }

    [Test]
    public void BitArray_ShiftRightFixed_WithInvalidCount_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true }).ShiftRightFixed(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true }).ShiftRightFixed(-10));
        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true }).ShiftRightFixed(2));
        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true }).ShiftRightFixed(10));

        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true, false, false, true }).ShiftRightFixed(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true, false, false, true }).ShiftRightFixed(-10));
        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true, false, false, true }).ShiftRightFixed(5));
        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true, false, false, true }).ShiftRightFixed(10));
    }

    [Test]
    public void Shift_WithInvalidCount_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.Shift(-2));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.Shift(-2, 100));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.Shift(-10));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.Shift(-10, 100));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.Shift(2));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.Shift(2, 100));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.Shift(10));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1 }.Shift(10, 100));

        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.Shift(-5));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.Shift(-5, 100));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.Shift(-10));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.Shift(-10, 100));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.Shift(5));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.Shift(5, 100));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.Shift(10));
        Should.Throw<ArgumentOutOfRangeException>(() => new[] { 1, 2, 3, 4 }.Shift(10, 100));
    }

    [Test]
    public void BitArray_Shift_WithInvalidCount_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true }).Shift(-2));
        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true }).Shift(-10));
        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true }).Shift(2));
        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true }).Shift(10));

        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true, false, false, true }).Shift(-5));
        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true, false, false, true }).Shift(-10));
        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true, false, false, true }).Shift(5));
        Should.Throw<ArgumentOutOfRangeException>(() => new BitArray(new[] { true, false, false, true }).Shift(10));
    }
}
