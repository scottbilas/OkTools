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
    public void BitArray_ShiftLeftRightFixed_ProperlyInsertsZeroes()
    {
        // force only the visible bits set (the SetAll func will do entire ints)
        void ManualSetAll(BitArray bits, bool value)
        {
            for (var i = 0; i < bits.Count; ++i)
                bits[i] = value;
        }

        // doing two things here:
        //
        // 1. validate behavior of BitArray `orig` (0 insertion from left, undefined from right)
        // 2. validate correct behavior of our own stuff `ours`
        // 3. validate our ops on a bool[] just for sanity sake

        // need to go past 32-bits to cross int boundaries

        var orig = new BitArray(40);
        ManualSetAll(orig, true);
        var ours = new BitArray(40);
        ManualSetAll(ours, true);
        var barr = new bool[40];
        Array.Fill(barr, true);

        // this should move the 20 bits off the end and insert zeroes, which we can check
        orig.LeftShift(20);
        orig.ToArray()[..20].All(b => b).ShouldBeFalse();
        ours.ShiftRightFixed(20);
        ours.ToArray()[..20].All(b => b).ShouldBeFalse();
        barr.ShiftRight(20, false);
        barr.ToArray()[..20].All(b => b).ShouldBeFalse();

        // everything true again, so the underlying ints should have 60 bits set
        ManualSetAll(orig, true);
        ManualSetAll(ours, true);
        Array.Fill(barr, true);

        // this is where behavior differs
        orig.RightShift(20);
        orig.ToArray()[^20..].Count(b => !b).ShouldBe(8); // shift, not zero insert
        ours.ShiftLeftFixed(20);
//        ours.ToArray()[^20..].Count(b => !b).ShouldBe(20);
        barr.ShiftLeft(20, false);
        barr.ToArray()[^20..].Count(b => !b).ShouldBe(20);
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
