using System.Runtime.CompilerServices;

// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo

static class OkListExtensions
{
    public static T[] PrivateArray<T>(this OkList<T> @this) => @this.AsArraySegment.Array!;
    public static T[] PrivateArray<T>(this OkDeList<T> @this) => @this.AsArraySegments.seg0.Array!;

    public static ref T PrivateRefAt<T>(this OkList<T> @this, int index) => ref @this.PrivateArray()[index];
    public static ref T PrivateRefAt<T>(this OkDeList<T> @this, int index) => ref @this.PrivateArray()[index];
}

partial class OkListTests
{
    static OkList<T> Make<T>(int capacity, params T[] items)
    {
        var list = new OkList<T>(capacity);
        list.AddRange(items);
        list.ToArray().ShouldBe(items);
        return list;
    }

    [Test]
    public void ValidateAssumptions()
    {
        RuntimeHelpers.IsReferenceOrContainsReferences<string>().ShouldBeTrue();
    }

    [Test]
    public void Ctor_WithPositiveValue_DoesNotThrow()
    {
        Should.NotThrow(() => Make<int>(0));
        Should.NotThrow(() => Make<int>(1));
        Should.NotThrow(() => Make<int>(1000));
    }

    [Test]
    public void Ctor_WithNegativeValue_Throws()
    {
        // note that this particular ctor throws an OverflowException rather than ArgumentOutOfRangeException because
        // `capacity` is passed directly to `new T[]` (and that intrinsic throws an OverflowException).

        Should.Throw<OverflowException>(() => new OkList<int>(-1));
        Should.Throw<OverflowException>(() => new OkList<int>(-1000));
    }

    [Test]
    public void Ctor_WithCountWithinCapacity_DoesNotThrow()
    {
        Should.NotThrow(() => new OkList<int>(5, 0));
        Should.NotThrow(() => new OkList<int>(5, 1));
        Should.NotThrow(() => new OkList<int>(5, 5));
    }

    [Test]
    public void Ctor_WithCountOutsideCapacity_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new OkList<int>(5, -1));
        Should.Throw<ArgumentOutOfRangeException>(() => new OkList<int>(5, -1000));
        Should.Throw<ArgumentOutOfRangeException>(() => new OkList<int>(5, 6));
        Should.Throw<ArgumentOutOfRangeException>(() => new OkList<int>(5, 100));
    }

    [Test]
    public void Ctor_WithNullCapacity_UsesCapacityFromCount()
    {
        var list = new OkList<int>(null, 0);
        list.Count.ShouldBe(0);
        list.Capacity.ShouldBe(0);

        list = new OkList<int>(null, 5);
        list.Count.ShouldBe(5);
        list.Capacity.ShouldBe(5);

        list = new OkList<int>(null, 1000);
        list.Count.ShouldBe(1000);
        list.Capacity.ShouldBe(1000);
    }

    [Test]
    public void Ctor_WithNullCapacityAndNegativeCount_Throws()
    {
        // note that this particular ctor throws an OverflowException rather than ArgumentOutOfRangeException because
        // `capacity` is passed directly to `new T[]` (and that intrinsic throws an OverflowException).

        Should.Throw<OverflowException>(() => new OkList<int>(null, -1));
        Should.Throw<OverflowException>(() => new OkList<int>(null, -1000));
    }

    [Test]
    public void IsEmptyAny_MatchesCount()
    {
        var list = new OkList<int>(10) { 0, 1, 2 };
        list.Any.ShouldBeTrue();
        list.IsEmpty.ShouldBeFalse();

        list.Clear();
        list.Any.ShouldBeFalse();
        list.IsEmpty.ShouldBeTrue();
    }

    [Test]
    public void SetCountOrClear_WithRefTypeAndReduction_FreesUnusedObjects()
    {
        var list1 = new OkList<string>(10) { "abc", "def" };
        list1.Count.ShouldBe(2);
        list1[1].ShouldBe("def");
        list1.Count = 1;
        list1.PrivateRefAt(1).ShouldBeNull();
        list1[0].ShouldBe("abc");
        list1.Clear();
        list1.PrivateRefAt(0).ShouldBeNull();

        var list2 = new OkList<string>(10) { "abc", "def" };
        list2.Count.ShouldBe(2);
        list2[1].ShouldBe("def");
        list2[0].ShouldBe("abc");
        list2.Clear();
        list2.Count.ShouldBe(0);
        list2.PrivateRefAt(0).ShouldBeNull();
        list2.PrivateRefAt(1).ShouldBeNull();
    }

    [Test]
    public void SetCountOrClear_WithValueTypeAndReduction_OnlyChangesCount()
    {
        var list1 = new OkList<int>(10) { 1, 2 };
        list1.Count.ShouldBe(2);
        list1[1].ShouldBe(2);
        list1.Count = 1;
        list1.PrivateRefAt(1).ShouldBe(2);
        list1[0].ShouldBe(1);
        list1.Clear();
        list1.PrivateRefAt(0).ShouldBe(1);

        var list2 = new OkList<int>(10) { 1, 2 };
        list2.Count.ShouldBe(2);
        list2[1].ShouldBe(2);
        list2[0].ShouldBe(1);
        list2.Clear();
        list2.Count.ShouldBe(0);
        list2.PrivateRefAt(0).ShouldBe(1);
        list2.PrivateRefAt(1).ShouldBe(2);
    }

    [Test]
    public void SetCount_WithValueTypeAndIncrease_ClearsNewItems()
    {
        var list = new OkList<int>(10) { 1, 2 };
        list.Count.ShouldBe(2);
        list[1].ShouldBe(2);
        list.Count = 1;
        list.PrivateRefAt(1).ShouldBe(2);
        list.Count = 2;
        list[1].ShouldBe(default);
        list[0].ShouldBe(1);
    }

    [Test]
    public void SetCount_WithIncreasePastCapacity_AllocsNewArray()
    {
        var list = new OkList<int>(2) { 1, 2 };
        list.Count.ShouldBe(2);
        list[1].ShouldBe(2);
        list.Count = 1;
        list.PrivateRefAt(1).ShouldBe(2);
        list.Count = 3;
        list[1].ShouldBe(default);
        list[0].ShouldBe(1);
        list.Capacity.ShouldBeGreaterThan(2);
    }

    [Test]
    public void SetCount_WithNegativeValue_Throws()
    {
        var list = new OkList<int>(10);
        Should.Throw<ArgumentOutOfRangeException>(() => list.Count = -1);
        Should.Throw<ArgumentOutOfRangeException>(() => list.Count = -1000);
    }

    [Test]
    public void SetCount_WithPositiveOrZeroValue_DoesNotThrow()
    {
        var list = new OkList<int>(10);
        Should.NotThrow(() => list.Count = 0);
        Should.NotThrow(() => list.Count = 1);
        Should.NotThrow(() => list.Count = 1000);
    }

    [Test]
    public void SetCountDirect_WithinCapacity_SetsCountWithoutInitializing()
    {
        var list = new OkList<string?>(5) { "abc", "def" };
        list.Count.ShouldBe(2);
        list.SetCountDirect(1);
        Validate(list, "abc");
        list.PrivateRefAt(1).ShouldBe("def");

        list.SetCountDirect(0);
        Validate(list);
        list.PrivateRefAt(0).ShouldBe("abc");
        list.PrivateRefAt(1).ShouldBe("def");

        list.SetCountDirect(5);
        Validate(list, "abc", "def", null, null, null);
        list.PrivateRefAt(0).ShouldBe("abc");
        list.PrivateRefAt(1).ShouldBe("def");
        list.PrivateRefAt(2).ShouldBeNull();
    }

    [Test]
    public void SetCountDirect_OutsideCapacity_Throws()
    {
        var list = new OkList<int>(10);
        Should.Throw<ArgumentOutOfRangeException>(() => list.SetCountDirect(11));
        Should.Throw<ArgumentOutOfRangeException>(() => list.SetCountDirect(-1));
    }

    // return true if clear with this trimCapacityTo causes a realloc
    static bool TestRealloc<T>(OkList<T> list, int trimCapacityTo)
    {
        var old = list.PrivateArray();

        list.Clear(trimCapacityTo);
        list.Count.ShouldBe(0);
        list.Capacity.ShouldBeLessThanOrEqualTo(trimCapacityTo);

        return !ReferenceEquals(old, list.PrivateArray());
    }

    [Test]
    public void Clear_WithTrimBelowCapacity_Reallocs()
    {
        TestRealloc(new OkList<int>(10) { 1, 2, 3, 4, 5 }, 9).ShouldBeTrue();
        TestRealloc(new OkList<int>(10) { 1, 2, 3, 4, 5 }, 3).ShouldBeTrue();
        TestRealloc(new OkList<int>(10) { 1, 2, 3, 4, 5 }, 1).ShouldBeTrue();
        TestRealloc(new OkList<int>(10) { 1, 2, 3, 4, 5 }, 0).ShouldBeTrue();
    }

    [Test]
    public void Clear_WithTrimAtOrAboveCapacity_DoesNotRealloc()
    {
        TestRealloc(new OkList<int>(10) { 1, 2, 3, 4, 5 }, 10).ShouldBeFalse();
        TestRealloc(new OkList<int>(10) { 1, 2, 3, 4, 5 }, 11).ShouldBeFalse();
        TestRealloc(new OkList<int>(10) { 1, 2, 3, 4, 5 }, 20).ShouldBeFalse();
    }

    [Test]
    public void FillVariants_WithEmptyList_DoesNotThrow()
    {
        var list = new OkList<int>(0);
        Should.NotThrow(() => list.Fill(7));
        Should.NotThrow(() => list.FillDefault());
    }

    [Test]
    public void FillVariants_WithValidList_FillsContents()
    {
        var list = new OkList<int>(5, 5);
        Validate(list, 0, 0, 0, 0, 0 );

        list.Fill(7);
        Validate(list, 7, 7, 7, 7, 7);

        list.FillDefault();
        Validate(list, 0, 0, 0, 0, 0);
    }

    [Test]
    public void EnumeratorGeneric()
    {
        new OkList<string>(10) { "abc", "def", "ghi" }.AsEnumerable().ToArray().ShouldBe(new[] { "abc", "def", "ghi" });
        new OkList<string>(10) { "abc" }.AsEnumerable().ToArray().ShouldBe(new[] { "abc" });
        new OkList<string>(10).AsEnumerable().ToArray().ShouldBeEmpty();
    }

    [Test]
    public void EnumeratorOldStyle()
    {
        static string[] ToArray(System.Collections.IEnumerable enu)
        {
            var list = new List<string>();
            foreach (string s in enu)
                list.Add(s);
            return list.ToArray();
        }

        ToArray(new OkList<string>(10) { "abc", "def", "ghi" }).ShouldBe(new[] { "abc", "def", "ghi" });
        ToArray(new OkList<string>(10) { "abc" }).ToArray().ShouldBe(new[] { "abc" });
        ToArray(new OkList<string>(10)).ToArray().ShouldBeEmpty();
    }

    [Test]
    public void Capacity_WithPositiveValue_DoesNotThrow()
    {
        var list = new OkList<int>(10);
        Should.NotThrow(() => list.Capacity = 0);
        Should.NotThrow(() => list.Capacity = 1);
        Should.NotThrow(() => list.Capacity = 1000);
    }

    [Test]
    public void Capacity_WithNegativeValue_Throws()
    {
        var list = new OkList<int>(10);
        Should.Throw<ArgumentOutOfRangeException>(() => list.Capacity = -1);
        Should.Throw<ArgumentOutOfRangeException>(() => list.Capacity = -1000);
    }

    [Test]
    public void Capacity_WithSameCapacity_DoesNotAlloc()
    {
        var list = new OkList<int>(10) { 1, 2 };
        list.Count = 1;
        list.PrivateRefAt(1).ShouldBe(2);
        list.Capacity = 10;
        list.PrivateRefAt(1).ShouldBe(2);
    }

    [Test]
    public void Capacity_WithGreaterCapacity_Reallocs()
    {
        var list = new OkList<int>(10) { 1, 2 };
        list.Capacity.ShouldBe(10);
        list.Count.ShouldBe(2);

        list.Count = 1;
        list.PrivateRefAt(1).ShouldBe(2);
        list.Capacity = 11;
        list.PrivateRefAt(1).ShouldBe(default);
        list.Capacity.ShouldBe(11);
    }

    [Test]
    public void Capacity_WithIncreaseViaAdd_GrowsByHalf()
    {
        var list = new OkList<int>(10) { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        list.Count.ShouldBe(10);
        list.Capacity.ShouldBe(10);

        list.Add(10);
        list.Count.ShouldBe(11);
        list.Capacity.ShouldBe(15);
    }

    [Test]
    public void Capacity_WithIncreaseViaCapacity_KeepsExact()
    {
        var list = new OkList<int>(10) { 0, 1, 2, 3, 4, 5, 6, 7 };
        list.Count.ShouldBe(8);
        list.Capacity.ShouldBe(10);

        list.Capacity = 11;
        list.Capacity.ShouldBe(11);

        list.Capacity = 13;
        list.Capacity.ShouldBe(13);
    }

    [Test]
    public void TrimCapacity_WithAlreadyTrimmed()
    {
        var list = new OkList<int>(10) { Count = 8 };
        list.Capacity.ShouldBe(10);
        list.TrimCapacity(1);
        list.Capacity.ShouldBe(10);
        list.TrimCapacity(11);
        list.Capacity.ShouldBe(10);
    }

    [Test]
    public void TrimCapacity_WithNeedsTrim()
    {
        var list = new OkList<int>(100) { Count = 4 };
        list.Capacity.ShouldBe(100);
        list.TrimCapacity(1);
        list.Capacity.ShouldBe(6);

        // check minimum
        list.Capacity = 100;
        list.TrimCapacity(10);
        list.Capacity.ShouldBe(10);
    }

    struct S { public int V; }

    [Test]
    public void Indexer_WithValidIndex()
    {
        var list = new OkList<S>(10) { new() { V = 1 }, new() { V = 2 }, new() { V = 3 }, new() { V = 4 } };
        list[0].V.ShouldBe(1);
        list[1].V.ShouldBe(2);
        list[2].V.ShouldBe(3);
        list[3].V.ShouldBe(4);
        list[3].V = 10;
        list[2].V = 20;
        list[1].V = 30;
        list[0].V = 40;
        list[0].V.ShouldBe(40);
        list[1].V.ShouldBe(30);
        list[2].V.ShouldBe(20);
        list[3].V.ShouldBe(10);
    }

    [Test]
    public void Indexer_WithInvalidIndex_Throws()
    {
        var list = new OkList<S>(10) { new() { V = 1 }, new() { V = 2 }, new() { V = 3 }, new() { V = 4 } };
        Should.Throw<ArgumentOutOfRangeException>(() => _ = list[ -1]);
        Should.Throw<ArgumentOutOfRangeException>(() => _ = list[-50]);
        Should.Throw<ArgumentOutOfRangeException>(() => _ = list[  4]);
        Should.Throw<ArgumentOutOfRangeException>(() => _ = list[100]);
        Should.Throw<ArgumentOutOfRangeException>(() => list[ -1].V = 42);
        Should.Throw<ArgumentOutOfRangeException>(() => list[-50].V = 42);
        Should.Throw<ArgumentOutOfRangeException>(() => list[  4].V = 42);
        Should.Throw<ArgumentOutOfRangeException>(() => list[100].V = 42);
    }

    [Test]
    public void Add()
    {
        var list = new OkList<int>(2);

        Validate(list);
        list.Add(1);
        Validate(list, 1);
        list.Add(2);
        Validate(list, 1, 2);
        list.Add(3);
        Validate(list, 1, 2, 3);

        list.Count = 1;
        Validate(list, 1);
    }

    [Test]
    public void AddRange_WithSpan()
    {
        var list = new OkList<int>(3);

        Validate(list);

        var array = new[] { 1, 2 }.AsSpan();
        list.AddRange(array);
        Validate(list, 1, 2);

        array = new[] { 3, 4, 5 }.AsSpan();
        list.AddRange(array);
        Validate(list, 1, 2, 3, 4, 5);

        array = Array.Empty<int>().AsSpan();
        list.AddRange(array);
        Validate(list, 1, 2, 3, 4, 5);
    }

    [Test]
    public void AddRange_WithArray()
    {
        var list = new OkList<int>(3);

        Validate(list);

        var array = new[] { 1, 2 };
        list.AddRange(array);
        Validate(list, 1, 2);

        array = new[] { 3, 4, 5 };
        list.AddRange(array);
        Validate(list, 1, 2, 3, 4, 5);

        array = Array.Empty<int>();
        list.AddRange(array);
        Validate(list, 1, 2, 3, 4, 5);
    }

    [Test]
    public void AddRange_WithParamsArray()
    {
        var list = new OkList<int>(3);

        Validate(list);

        list.AddRange(1, 2);
        Validate(list, 1, 2);

        list.AddRange(3, 4, 5);
        Validate(list, 1, 2, 3, 4, 5);

        list.AddRange();
        Validate(list, 1, 2, 3, 4, 5);
    }

    [Test]
    public void RemoveAtAndSwapBack_WithInvalidIndex_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new OkList<int>(null, 0).RemoveAtAndSwapBack(0));
        Should.Throw<ArgumentOutOfRangeException>(() => new OkList<int>(null, 5).RemoveAtAndSwapBack(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => new OkList<int>(null, 5).RemoveAtAndSwapBack(-100));
        Should.Throw<ArgumentOutOfRangeException>(() => new OkList<int>(null, 5).RemoveAtAndSwapBack(5));
        Should.Throw<ArgumentOutOfRangeException>(() => new OkList<int>(null, 5).RemoveAtAndSwapBack(6));
        Should.Throw<ArgumentOutOfRangeException>(() => new OkList<int>(null, 5).RemoveAtAndSwapBack(100));
    }

    [Test]
    public void RemoveAtAndSwapBack()
    {
        var list = new OkList<int>(10) { 1, 2, 3 };
        list.RemoveAtAndSwapBack(0);
        Validate(list, 3, 2);
        list.RemoveAtAndSwapBack(0);
        Validate(list, 2);
        list.RemoveAtAndSwapBack(0);
        Validate(list);

        list = new OkList<int>(10) { 1, 2, 3 };
        list.RemoveAtAndSwapBack(2);
        Validate(list, 1, 2);
        list.RemoveAtAndSwapBack(1);
        Validate(list, 1);
        list.RemoveAtAndSwapBack(0);
        Validate(list);
    }

    [Test]
    public void DropBack_WithEmpty_Throws()
    {
        Should.Throw<InvalidOperationException>(() => new OkList<int>(null, 0).DropBack());
    }

    [Test]
    public void DropBack_WithNonEmpty_Removes()
    {
        var list = new OkList<int>(10) { 0, 1, 2, 3, 4, 5 };
        list[^1].ShouldBe(5);
        list.DropBack();
        list[^1].ShouldBe(4);
        list.DropBack();
        list[^1].ShouldBe(3);
        list.DropBack();
        list[^1].ShouldBe(2);
        list.DropBack();
        list[^1].ShouldBe(1);
        list.DropBack();
        list[^1].ShouldBe(0);
        list.DropBack();
        list.IsEmpty.ShouldBeTrue();
    }

    [Test]
    public void PopBack_WithEmpty_Throws()
    {
        Should.Throw<InvalidOperationException>(() => new OkList<int>(null, 0).PopBack());
    }

    [Test]
    public void PopBack_WithNonEmpty_RemovesAndReturnsItem()
    {
        var list = new OkList<int>(10) { 0, 1, 2, 3, 4, 5 };
        list[^1].ShouldBe(5);
        list.PopBack().ShouldBe(5);
        list[^1].ShouldBe(4);
        list.PopBack().ShouldBe(4);
        list[^1].ShouldBe(3);
        list.PopBack().ShouldBe(3);
        list[^1].ShouldBe(2);
        list.PopBack().ShouldBe(2);
        list[^1].ShouldBe(1);
        list.PopBack().ShouldBe(1);
        list[^1].ShouldBe(0);
        list.PopBack().ShouldBe(0);
        list.IsEmpty.ShouldBeTrue();
    }

    [Test]
    public void PopAndDropAndSwapBack_UseUnderlyingCountClear()
    {
        // these ensure that we're using underlying valuetype-sensitive clear

        var vlist = new OkList<int>(10) { 0, 1, 2, 3, 4, 5 };
        vlist.DropBack();
        vlist.PopBack().ShouldBe(4);
        vlist.RemoveAtAndSwapBack(1);
        Validate(vlist, 0, 3, 2 );
        vlist.PrivateRefAt(3).ShouldBe(3);
        vlist.PrivateRefAt(4).ShouldBe(4);
        vlist.PrivateRefAt(5).ShouldBe(5);

        var rlist = new OkList<string>(10) { "a", "b", "c", "d", "e", "f" };
        rlist.DropBack();
        rlist.PopBack().ShouldBe("e");
        rlist.RemoveAtAndSwapBack(1);
        Validate(rlist, "a", "d", "c");
        rlist.PrivateRefAt(3).ShouldBeNull();
        rlist.PrivateRefAt(4).ShouldBeNull();
        rlist.PrivateRefAt(5).ShouldBeNull();
    }
}

partial class OkDeListTests
{
    static OkDeList<T> Make<T>(int capacity, params T[] items)
    {
        var list = new OkDeList<T>(capacity);
        list.AddRange(items[(list.Count/2)..]);
        list.AddRangeFront(items[..list.Count]);
        list.ToArray().ShouldBe(items);
        return list;
    }

    [Test]
    public void ValidateAssumptions()
    {
        RuntimeHelpers.IsReferenceOrContainsReferences<string>().ShouldBeTrue();
    }

    [Test]
    public void Ctor_WithPositiveValue_DoesNotThrow()
    {
        Should.NotThrow(() => Make<int>(0));
        Should.NotThrow(() => Make<int>(1));
        Should.NotThrow(() => Make<int>(1000));
    }

    [Test]
    public void Ctor_WithNegativeValue_Throws()
    {
        // note that this particular ctor throws an OverflowException rather than ArgumentOutOfRangeException because
        // `capacity` is passed directly to `new T[]` (and that intrinsic throws an OverflowException).

        Should.Throw<OverflowException>(() => new OkDeList<int>(-1));
        Should.Throw<OverflowException>(() => new OkDeList<int>(-1000));
    }

    [Test]
    public void Ctor_WithCountWithinCapacity_DoesNotThrow()
    {
        Should.NotThrow(() => new OkDeList<int>(5, 0));
        Should.NotThrow(() => new OkDeList<int>(5, 1));
        Should.NotThrow(() => new OkDeList<int>(5, 5));
    }

    [Test]
    public void Ctor_WithCountOutsideCapacity_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new OkDeList<int>(5, -1));
        Should.Throw<ArgumentOutOfRangeException>(() => new OkDeList<int>(5, -1000));
        Should.Throw<ArgumentOutOfRangeException>(() => new OkDeList<int>(5, 6));
        Should.Throw<ArgumentOutOfRangeException>(() => new OkDeList<int>(5, 100));
    }

    [Test]
    public void Ctor_WithNullCapacity_UsesCapacityFromCount()
    {
        var list = new OkDeList<int>(null, 0);
        list.Count.ShouldBe(0);
        list.Capacity.ShouldBe(0);

        list = new OkDeList<int>(null, 5);
        list.Count.ShouldBe(5);
        list.Capacity.ShouldBe(5);

        list = new OkDeList<int>(null, 1000);
        list.Count.ShouldBe(1000);
        list.Capacity.ShouldBe(1000);
    }

    [Test]
    public void Ctor_WithNullCapacityAndNegativeCount_Throws()
    {
        // note that this particular ctor throws an OverflowException rather than ArgumentOutOfRangeException because
        // `capacity` is passed directly to `new T[]` (and that intrinsic throws an OverflowException).

        Should.Throw<OverflowException>(() => new OkDeList<int>(null, -1));
        Should.Throw<OverflowException>(() => new OkDeList<int>(null, -1000));
    }

    [Test]
    public void IsEmptyAny_MatchesCount()
    {
        var list = new OkDeList<int>(10) { 0, 1, 2 };
        list.Any.ShouldBeTrue();
        list.IsEmpty.ShouldBeFalse();

        list.Clear();
        list.Any.ShouldBeFalse();
        list.IsEmpty.ShouldBeTrue();
    }

    [Test]
    public void SetCountOrClear_WithRefTypeAndReduction_FreesUnusedObjects()
    {
        var list1 = new OkDeList<string>(10) { "abc", "def" };
        list1.Count.ShouldBe(2);
        list1[1].ShouldBe("def");
        list1.Count = 1;
        list1.PrivateRefAt(1).ShouldBeNull();
        list1[0].ShouldBe("abc");
        list1.Clear();
        list1.PrivateRefAt(0).ShouldBeNull();

        var list2 = new OkDeList<string>(10) { "abc", "def" };
        list2.Count.ShouldBe(2);
        list2[1].ShouldBe("def");
        list2[0].ShouldBe("abc");
        list2.Clear();
        list2.Count.ShouldBe(0);
        list2.PrivateRefAt(0).ShouldBeNull();
        list2.PrivateRefAt(1).ShouldBeNull();
    }

    [Test]
    public void SetCountOrClear_WithValueTypeAndReduction_OnlyChangesCount()
    {
        var list1 = new OkDeList<int>(10) { 1, 2 };
        list1.Count.ShouldBe(2);
        list1[1].ShouldBe(2);
        list1.Count = 1;
        list1.PrivateRefAt(1).ShouldBe(2);
        list1[0].ShouldBe(1);
        list1.Clear();
        list1.PrivateRefAt(0).ShouldBe(1);

        var list2 = new OkDeList<int>(10) { 1, 2 };
        list2.Count.ShouldBe(2);
        list2[1].ShouldBe(2);
        list2[0].ShouldBe(1);
        list2.Clear();
        list2.Count.ShouldBe(0);
        list2.PrivateRefAt(0).ShouldBe(1);
        list2.PrivateRefAt(1).ShouldBe(2);
    }

    [Test]
    public void SetCount_WithValueTypeAndIncrease_ClearsNewItems()
    {
        var list = new OkDeList<int>(10) { 1, 2 };
        list.Count.ShouldBe(2);
        list[1].ShouldBe(2);
        list.Count = 1;
        list.PrivateRefAt(1).ShouldBe(2);
        list.Count = 2;
        list[1].ShouldBe(default);
        list[0].ShouldBe(1);
    }

    [Test]
    public void SetCount_WithIncreasePastCapacity_AllocsNewArray()
    {
        var list = new OkDeList<int>(2) { 1, 2 };
        list.Count.ShouldBe(2);
        list[1].ShouldBe(2);
        list.Count = 1;
        list.PrivateRefAt(1).ShouldBe(2);
        list.Count = 3;
        list[1].ShouldBe(default);
        list[0].ShouldBe(1);
        list.Capacity.ShouldBeGreaterThan(2);
    }

    [Test]
    public void SetCount_WithNegativeValue_Throws()
    {
        var list = new OkDeList<int>(10);
        Should.Throw<ArgumentOutOfRangeException>(() => list.Count = -1);
        Should.Throw<ArgumentOutOfRangeException>(() => list.Count = -1000);
    }

    [Test]
    public void SetCount_WithPositiveOrZeroValue_DoesNotThrow()
    {
        var list = new OkDeList<int>(10);
        Should.NotThrow(() => list.Count = 0);
        Should.NotThrow(() => list.Count = 1);
        Should.NotThrow(() => list.Count = 1000);
    }

    [Test]
    public void SetCountDirect_WithinCapacity_SetsCountWithoutInitializing()
    {
        var list = new OkDeList<string?>(5) { "abc", "def" };
        list.Count.ShouldBe(2);
        list.SetCountDirect(1);
        Validate(list, "abc");
        list.PrivateRefAt(1).ShouldBe("def");

        list.SetCountDirect(0);
        Validate(list);
        list.PrivateRefAt(0).ShouldBe("abc");
        list.PrivateRefAt(1).ShouldBe("def");

        list.SetCountDirect(5);
        Validate(list, "abc", "def", null, null, null);
        list.PrivateRefAt(0).ShouldBe("abc");
        list.PrivateRefAt(1).ShouldBe("def");
        list.PrivateRefAt(2).ShouldBeNull();
    }

    [Test]
    public void SetCountDirect_OutsideCapacity_Throws()
    {
        var list = new OkDeList<int>(10);
        Should.Throw<ArgumentOutOfRangeException>(() => list.SetCountDirect(11));
        Should.Throw<ArgumentOutOfRangeException>(() => list.SetCountDirect(-1));
    }

    // return true if clear with this trimCapacityTo causes a realloc
    static bool TestRealloc<T>(OkDeList<T> list, int trimCapacityTo)
    {
        var old = list.PrivateArray();

        list.Clear(trimCapacityTo);
        list.Count.ShouldBe(0);
        list.Capacity.ShouldBeLessThanOrEqualTo(trimCapacityTo);

        return !ReferenceEquals(old, list.PrivateArray());
    }

    [Test]
    public void Clear_WithTrimBelowCapacity_Reallocs()
    {
        TestRealloc(new OkDeList<int>(10) { 1, 2, 3, 4, 5 }, 9).ShouldBeTrue();
        TestRealloc(new OkDeList<int>(10) { 1, 2, 3, 4, 5 }, 3).ShouldBeTrue();
        TestRealloc(new OkDeList<int>(10) { 1, 2, 3, 4, 5 }, 1).ShouldBeTrue();
        TestRealloc(new OkDeList<int>(10) { 1, 2, 3, 4, 5 }, 0).ShouldBeTrue();
    }

    [Test]
    public void Clear_WithTrimAtOrAboveCapacity_DoesNotRealloc()
    {
        TestRealloc(new OkDeList<int>(10) { 1, 2, 3, 4, 5 }, 10).ShouldBeFalse();
        TestRealloc(new OkDeList<int>(10) { 1, 2, 3, 4, 5 }, 11).ShouldBeFalse();
        TestRealloc(new OkDeList<int>(10) { 1, 2, 3, 4, 5 }, 20).ShouldBeFalse();
    }

    [Test]
    public void FillVariants_WithEmptyList_DoesNotThrow()
    {
        var list = new OkDeList<int>(0);
        Should.NotThrow(() => list.Fill(7));
        Should.NotThrow(() => list.FillDefault());
    }

    [Test]
    public void FillVariants_WithValidList_FillsContents()
    {
        var list = new OkDeList<int>(5, 5);
        Validate(list, 0, 0, 0, 0, 0 );

        list.Fill(7);
        Validate(list, 7, 7, 7, 7, 7);

        list.FillDefault();
        Validate(list, 0, 0, 0, 0, 0);
    }

    [Test]
    public void EnumeratorGeneric()
    {
        new OkDeList<string>(10) { "abc", "def", "ghi" }.AsEnumerable().ToArray().ShouldBe(new[] { "abc", "def", "ghi" });
        new OkDeList<string>(10) { "abc" }.AsEnumerable().ToArray().ShouldBe(new[] { "abc" });
        new OkDeList<string>(10).AsEnumerable().ToArray().ShouldBeEmpty();
    }

    [Test]
    public void EnumeratorOldStyle()
    {
        static string[] ToArray(System.Collections.IEnumerable enu)
        {
            var list = new List<string>();
            foreach (string s in enu)
                list.Add(s);
            return list.ToArray();
        }

        ToArray(new OkDeList<string>(10) { "abc", "def", "ghi" }).ShouldBe(new[] { "abc", "def", "ghi" });
        ToArray(new OkDeList<string>(10) { "abc" }).ToArray().ShouldBe(new[] { "abc" });
        ToArray(new OkDeList<string>(10)).ToArray().ShouldBeEmpty();
    }

    [Test]
    public void Capacity_WithPositiveValue_DoesNotThrow()
    {
        var list = new OkDeList<int>(10);
        Should.NotThrow(() => list.Capacity = 0);
        Should.NotThrow(() => list.Capacity = 1);
        Should.NotThrow(() => list.Capacity = 1000);
    }

    [Test]
    public void Capacity_WithNegativeValue_Throws()
    {
        var list = new OkDeList<int>(10);
        Should.Throw<ArgumentOutOfRangeException>(() => list.Capacity = -1);
        Should.Throw<ArgumentOutOfRangeException>(() => list.Capacity = -1000);
    }

    [Test]
    public void Capacity_WithSameCapacity_DoesNotAlloc()
    {
        var list = new OkDeList<int>(10) { 1, 2 };
        list.Count = 1;
        list.PrivateRefAt(1).ShouldBe(2);
        list.Capacity = 10;
        list.PrivateRefAt(1).ShouldBe(2);
    }

    [Test]
    public void Capacity_WithGreaterCapacity_Reallocs()
    {
        var list = new OkDeList<int>(10) { 1, 2 };
        list.Capacity.ShouldBe(10);
        list.Count.ShouldBe(2);

        list.Count = 1;
        list.PrivateRefAt(1).ShouldBe(2);
        list.Capacity = 11;
        list.PrivateRefAt(1).ShouldBe(default);
        list.Capacity.ShouldBe(11);
    }

    [Test]
    public void Capacity_WithIncreaseViaAdd_GrowsByHalf()
    {
        var list = new OkDeList<int>(10) { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        list.Count.ShouldBe(10);
        list.Capacity.ShouldBe(10);

        list.Add(10);
        list.Count.ShouldBe(11);
        list.Capacity.ShouldBe(15);
    }

    [Test]
    public void Capacity_WithIncreaseViaCapacity_KeepsExact()
    {
        var list = new OkDeList<int>(10) { 0, 1, 2, 3, 4, 5, 6, 7 };
        list.Count.ShouldBe(8);
        list.Capacity.ShouldBe(10);

        list.Capacity = 11;
        list.Capacity.ShouldBe(11);

        list.Capacity = 13;
        list.Capacity.ShouldBe(13);
    }

    [Test]
    public void TrimCapacity_WithAlreadyTrimmed()
    {
        var list = new OkDeList<int>(10) { Count = 8 };
        list.Capacity.ShouldBe(10);
        list.TrimCapacity(1);
        list.Capacity.ShouldBe(10);
        list.TrimCapacity(11);
        list.Capacity.ShouldBe(10);
    }

    [Test]
    public void TrimCapacity_WithNeedsTrim()
    {
        var list = new OkDeList<int>(100) { Count = 4 };
        list.Capacity.ShouldBe(100);
        list.TrimCapacity(1);
        list.Capacity.ShouldBe(6);

        // check minimum
        list.Capacity = 100;
        list.TrimCapacity(10);
        list.Capacity.ShouldBe(10);
    }

    struct S { public int V; }

    [Test]
    public void Indexer_WithValidIndex()
    {
        var list = new OkDeList<S>(10) { new() { V = 1 }, new() { V = 2 }, new() { V = 3 }, new() { V = 4 } };
        list[0].V.ShouldBe(1);
        list[1].V.ShouldBe(2);
        list[2].V.ShouldBe(3);
        list[3].V.ShouldBe(4);
        list[3].V = 10;
        list[2].V = 20;
        list[1].V = 30;
        list[0].V = 40;
        list[0].V.ShouldBe(40);
        list[1].V.ShouldBe(30);
        list[2].V.ShouldBe(20);
        list[3].V.ShouldBe(10);
    }

    [Test]
    public void Indexer_WithInvalidIndex_Throws()
    {
        var list = new OkDeList<S>(10) { new() { V = 1 }, new() { V = 2 }, new() { V = 3 }, new() { V = 4 } };
        Should.Throw<ArgumentOutOfRangeException>(() => _ = list[ -1]);
        Should.Throw<ArgumentOutOfRangeException>(() => _ = list[-50]);
        Should.Throw<ArgumentOutOfRangeException>(() => _ = list[  4]);
        Should.Throw<ArgumentOutOfRangeException>(() => _ = list[100]);
        Should.Throw<ArgumentOutOfRangeException>(() => list[ -1].V = 42);
        Should.Throw<ArgumentOutOfRangeException>(() => list[-50].V = 42);
        Should.Throw<ArgumentOutOfRangeException>(() => list[  4].V = 42);
        Should.Throw<ArgumentOutOfRangeException>(() => list[100].V = 42);
    }

    [Test]
    public void Add()
    {
        var list = new OkDeList<int>(2);

        Validate(list);
        list.Add(1);
        Validate(list, 1);
        list.Add(2);
        Validate(list, 1, 2);
        list.Add(3);
        Validate(list, 1, 2, 3);

        list.Count = 1;
        Validate(list, 1);
    }

    [Test]
    public void AddRange_WithSpan()
    {
        var list = new OkDeList<int>(3);

        Validate(list);

        var array = new[] { 1, 2 }.AsSpan();
        list.AddRange(array);
        Validate(list, 1, 2);

        array = new[] { 3, 4, 5 }.AsSpan();
        list.AddRange(array);
        Validate(list, 1, 2, 3, 4, 5);

        array = Array.Empty<int>().AsSpan();
        list.AddRange(array);
        Validate(list, 1, 2, 3, 4, 5);
    }

    [Test]
    public void AddRange_WithArray()
    {
        var list = new OkDeList<int>(3);

        Validate(list);

        var array = new[] { 1, 2 };
        list.AddRange(array);
        Validate(list, 1, 2);

        array = new[] { 3, 4, 5 };
        list.AddRange(array);
        Validate(list, 1, 2, 3, 4, 5);

        array = Array.Empty<int>();
        list.AddRange(array);
        Validate(list, 1, 2, 3, 4, 5);
    }

    [Test]
    public void AddRange_WithParamsArray()
    {
        var list = new OkDeList<int>(3);

        Validate(list);

        list.AddRange(1, 2);
        Validate(list, 1, 2);

        list.AddRange(3, 4, 5);
        Validate(list, 1, 2, 3, 4, 5);

        list.AddRange();
        Validate(list, 1, 2, 3, 4, 5);
    }

    [Test]
    public void RemoveAtAndSwapBack_WithInvalidIndex_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new OkDeList<int>(null, 0).RemoveAtAndSwapBack(0));
        Should.Throw<ArgumentOutOfRangeException>(() => new OkDeList<int>(null, 5).RemoveAtAndSwapBack(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => new OkDeList<int>(null, 5).RemoveAtAndSwapBack(-100));
        Should.Throw<ArgumentOutOfRangeException>(() => new OkDeList<int>(null, 5).RemoveAtAndSwapBack(5));
        Should.Throw<ArgumentOutOfRangeException>(() => new OkDeList<int>(null, 5).RemoveAtAndSwapBack(6));
        Should.Throw<ArgumentOutOfRangeException>(() => new OkDeList<int>(null, 5).RemoveAtAndSwapBack(100));
    }

    [Test]
    public void RemoveAtAndSwapBack()
    {
        var list = new OkDeList<int>(10) { 1, 2, 3 };
        list.RemoveAtAndSwapBack(0);
        Validate(list, 3, 2);
        list.RemoveAtAndSwapBack(0);
        Validate(list, 2);
        list.RemoveAtAndSwapBack(0);
        Validate(list);

        list = new OkDeList<int>(10) { 1, 2, 3 };
        list.RemoveAtAndSwapBack(2);
        Validate(list, 1, 2);
        list.RemoveAtAndSwapBack(1);
        Validate(list, 1);
        list.RemoveAtAndSwapBack(0);
        Validate(list);
    }

    [Test]
    public void DropBack_WithEmpty_Throws()
    {
        Should.Throw<InvalidOperationException>(() => new OkDeList<int>(null, 0).DropBack());
    }

    [Test]
    public void DropBack_WithNonEmpty_Removes()
    {
        var list = new OkDeList<int>(10) { 0, 1, 2, 3, 4, 5 };
        list[^1].ShouldBe(5);
        list.DropBack();
        list[^1].ShouldBe(4);
        list.DropBack();
        list[^1].ShouldBe(3);
        list.DropBack();
        list[^1].ShouldBe(2);
        list.DropBack();
        list[^1].ShouldBe(1);
        list.DropBack();
        list[^1].ShouldBe(0);
        list.DropBack();
        list.IsEmpty.ShouldBeTrue();
    }

    [Test]
    public void PopBack_WithEmpty_Throws()
    {
        Should.Throw<InvalidOperationException>(() => new OkDeList<int>(null, 0).PopBack());
    }

    [Test]
    public void PopBack_WithNonEmpty_RemovesAndReturnsItem()
    {
        var list = new OkDeList<int>(10) { 0, 1, 2, 3, 4, 5 };
        list[^1].ShouldBe(5);
        list.PopBack().ShouldBe(5);
        list[^1].ShouldBe(4);
        list.PopBack().ShouldBe(4);
        list[^1].ShouldBe(3);
        list.PopBack().ShouldBe(3);
        list[^1].ShouldBe(2);
        list.PopBack().ShouldBe(2);
        list[^1].ShouldBe(1);
        list.PopBack().ShouldBe(1);
        list[^1].ShouldBe(0);
        list.PopBack().ShouldBe(0);
        list.IsEmpty.ShouldBeTrue();
    }

    [Test]
    public void PopAndDropAndSwapBack_UseUnderlyingCountClear()
    {
        // these ensure that we're using underlying valuetype-sensitive clear

        var vlist = new OkDeList<int>(10) { 0, 1, 2, 3, 4, 5 };
        vlist.DropBack();
        vlist.PopBack().ShouldBe(4);
        vlist.RemoveAtAndSwapBack(1);
        Validate(vlist, 0, 3, 2 );
        vlist.PrivateRefAt(3).ShouldBe(3);
        vlist.PrivateRefAt(4).ShouldBe(4);
        vlist.PrivateRefAt(5).ShouldBe(5);

        var rlist = new OkDeList<string>(10) { "a", "b", "c", "d", "e", "f" };
        rlist.DropBack();
        rlist.PopBack().ShouldBe("e");
        rlist.RemoveAtAndSwapBack(1);
        Validate(rlist, "a", "d", "c");
        rlist.PrivateRefAt(3).ShouldBeNull();
        rlist.PrivateRefAt(4).ShouldBeNull();
        rlist.PrivateRefAt(5).ShouldBeNull();
    }
}
