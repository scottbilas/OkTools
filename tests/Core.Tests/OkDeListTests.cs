// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo

partial class OkDeListTests
{
    //           cap  h  u b0 e0 b1 e1 c0 f0 c1 f1
    [TestCase('a', 9, 0, 0, 0, 0, 0, 0, 0, 9, 0, 0)] // [h........]
    [TestCase('b', 9, 2, 0, 2, 2, 0, 0, 2, 9, 0, 2)] // [..h......]
    [TestCase('c', 9, 0, 6, 0, 6, 0, 0, 6, 9, 0, 0)] // [h12345...]
    [TestCase('d', 9, 0, 9, 0, 9, 0, 0, 0, 0, 0, 0)] // [h12345678]
    [TestCase('e', 9, 2, 7, 2, 9, 0, 0, 0, 2, 0, 0)] // [..h123456]
    [TestCase('f', 9, 2, 9, 2, 9, 0, 2, 0, 0, 0, 0)] // [78h123456]
    [TestCase('g', 9, 4, 7, 4, 9, 0, 2, 2, 4, 0, 0)] // [56..h1234]
    [TestCase('h', 9, 8, 7, 8, 9, 0, 6, 6, 8, 0, 0)] // [123456..h]
    public void DeBasics(char _, // just for sorting in unit test explorer
        int capacity, int head, int used,
        int b0, int e0, int b1, int e1, // used
        int c0, int f0, int c1, int f1) // unused
    {
        // meh i just don't like exposing stuff with `internals`
        static OkDeList<T> MakeDirect<T>(T[] items, int head, int used)
        {
            var list = new OkDeList<T>(0);
            list.PrivateSetItems(items);
            list.PrivateSetHead(head);
            list.PrivateSetUsed(used);
            return list;
        }

        // make

        var array = Enumerable.Range(0, capacity).ToArray();
        var list = MakeDirect(array, head, used);

        // used spans

        var spans = list.AsSpans;
        spans.Span0.Length.ShouldBe(e0 - b0);
        spans.Span1.Length.ShouldBe(e1 - b1);

        // used mems

        var mems = list.AsMemories;
        mems.mem0.Length.ShouldBe(e0 - b0);
        mems.mem1.Length.ShouldBe(e1 - b1);

        // can't check span/mem offsets like we can array segs, so use element contents
        if (b0 != e0)
        {
            spans.Span0[0].ShouldBe(b0);
            mems.mem0.Span[0].ShouldBe(b0);
        }
        if (b1 != e1)
        {
            spans.Span1[0].ShouldBe(b1);
            mems.mem1.Span[0].ShouldBe(b1);
        }

        // used segs

        var segs = list.AsArraySegments;
        segs.seg0.Array.ShouldBe(array);
        segs.seg0.Offset.ShouldBe(b0);
        segs.seg0.Count.ShouldBe(e0 - b0);
        segs.seg1.Array?.ShouldBe(array); // null if no wrap segment
        segs.seg1.Offset.ShouldBe(b1);
        segs.seg1.Count.ShouldBe(e1 - b1);

        // unused spans

        var unused = list.UnusedSpans;
        unused.Span0.Length.ShouldBe(f0 - c0);
        unused.Span1.Length.ShouldBe(f1 - c1);

        if (c0 != f0)
            unused.Span0[0].ShouldBe(c0);
        if (c1 != f1)
            unused.Span1[0].ShouldBe(c1);
    }

    [Test]
    public void AddRange_Wrap()
    {
        var list = new OkDeList<int>(5) { 1, 2, 3 };
        list.DropFront();
        list.DropFront();
        list.AddRange(4, 5, 6);

        list.AsArraySegments.seg0.ShouldBe(new[] { 3, 4, 5 });
        list.AsArraySegments.seg0.ShouldBe(new ArraySegment<int>(list.PrivateArray(), 2, 3));
        list.AsArraySegments.seg1.ShouldBe(new[] { 6 });
        list.AsArraySegments.seg1.ShouldBe(new ArraySegment<int>(list.PrivateArray(), 0, 1));
    }

    [Test]
    public void AddFront()
    {
        var list = new OkDeList<int>(5) { 1, 2, 3 };
        list.PrivateRefAt(0).ShouldBe(1);
        Validate(list, 1, 2, 3);
        list.Capacity.ShouldBe(5);

        // open a space at real index 0
        list.DropFront();
        Validate(list, 2, 3);
        list.Capacity.ShouldBe(5);

        // fill space 0
        list.AddFront(4);
        list.PrivateRefAt(0).ShouldBe(4);
        Validate(list, 4, 2, 3);
        list.Capacity.ShouldBe(5);

        // wrap
        list.AddFront(5);
        list.PrivateRefAt(4).ShouldBe(5);
        Validate(list, 5, 4, 2, 3);
        list.Capacity.ShouldBe(5);

        list.AddFront(6);
        list.PrivateRefAt(3).ShouldBe(6);
        Validate(list, 6, 5, 4, 2, 3);
        list.Capacity.ShouldBe(5);

        // expand
        list.AddFront(7);
        list.PrivateRefAt(0).ShouldBe(7); // re-pack
        Validate(list, 7, 6, 5, 4, 2, 3);
        list.Capacity.ShouldBe(7);

        list.Count = 3;
        list.PrivateRefAt(0).ShouldBe(7);
        Validate(list, 7, 6, 5);
        list.Capacity.ShouldBe(7);
    }

    [Test]
    public void AddRangeFront()
    {
        var list = Make<int>(5);
        Validate(list);

        // only one span, at the end
        list.AddRangeFront(1, 2);
        Validate(list, 1, 2);
        list.AsArraySegments.seg0.ShouldBe(new[] { 1, 2 });
        list.AsArraySegments.seg0.ShouldBe(new ArraySegment<int>(list.PrivateArray(), 3, 2));
        list.AsArraySegments.seg1.Array.ShouldBeNull();

        // make it straddle
        list.AddRange(3, 4);
        Validate(list, 1, 2, 3, 4);
        list.AsArraySegments.seg0.ShouldBe(new[] { 1, 2 });
        list.AsArraySegments.seg0.ShouldBe(new ArraySegment<int>(list.PrivateArray(), 3, 2));
        list.AsArraySegments.seg1.ShouldBe(new[] { 3, 4 });
        list.AsArraySegments.seg1.ShouldBe(new ArraySegment<int>(list.PrivateArray(), 0, 2));

        // this will realloc and repack
        list.AddRangeFront(5, 6);
        Validate(list, 5, 6, 1, 2, 3, 4);
        list.AsArraySegments.seg0.ShouldBe(new[] { 5, 6, 1, 2, 3, 4 });
        list.AsArraySegments.seg0.ShouldBe(new ArraySegment<int>(list.PrivateArray(), 0, 6));
        list.AsArraySegments.seg1.Array.ShouldBeNull();

        // make some space
        list.DropFront();
        list.DropFront();
        Validate(list, 1, 2, 3, 4);
        list.AsArraySegments.seg0.ShouldBe(new[] { 1, 2, 3, 4 });
        list.AsArraySegments.seg0.ShouldBe(new ArraySegment<int>(list.PrivateArray(), 2, 4));
        list.AsArraySegments.seg1.Array.ShouldBeNull();

        // fill in till before wrap
        list.AddRangeFront(7, 8);
        Validate(list, 7, 8, 1, 2, 3, 4);
        list.AsArraySegments.seg0.ShouldBe(new[] { 7, 8, 1, 2, 3, 4 });
        list.AsArraySegments.seg0.ShouldBe(new ArraySegment<int>(list.PrivateArray(), 0, 6));
        list.AsArraySegments.seg1.Array.ShouldBeNull();

        // ok now make space at both ends
        list.DropFront();
        list.DropFront();
        list.Count = 2;
        Validate(list, 1, 2);
        list.AsArraySegments.seg0.ShouldBe(new[] { 1, 2 });
        list.AsArraySegments.seg0.ShouldBe(new ArraySegment<int>(list.PrivateArray(), 2, 2));
        list.AsArraySegments.seg1.Array.ShouldBeNull();

        // finally do a front add that wraps
        list.AddRangeFront(9, 10, 11, 12);
        list.Capacity.ShouldBe(7);
        Validate(list, 9, 10, 11, 12, 1, 2);
        list.AsArraySegments.seg0.ShouldBe(new[] { 9, 10 });
        list.AsArraySegments.seg0.ShouldBe(new ArraySegment<int>(list.PrivateArray(), 5, 2));
        list.AsArraySegments.seg1.ShouldBe(new[] { 11, 12, 1, 2 });
        list.AsArraySegments.seg1.ShouldBe(new ArraySegment<int>(list.PrivateArray(), 0, 4));
    }

    [Test]
    public void AddRangeFront_WithSpan()
    {
        var list = Make<int>(4);
        Validate(list);

        var span = new[] { 1, 2 }.AsSpan();
        list.AddRangeFront(span);
        Validate(list, 1, 2);

        span = new[] { 3, 4, 5 }.AsSpan();
        list.AddRangeFront(span);
        Validate(list, 3, 4, 5, 1, 2);

        span = Array.Empty<int>().AsSpan();
        list.AddRangeFront(span);
        Validate(list, 3, 4, 5, 1, 2);
    }

    [Test]
    public void AddRangeFront_WithArray()
    {
        var list = Make<int>(4);
        Validate(list);

        var array = new[] { 1, 2 };
        list.AddRangeFront(array);
        Validate(list, 1, 2);

        array = new[] { 3, 4, 5 };
        list.AddRangeFront(array);
        Validate(list, 3, 4, 5, 1, 2);

        array = Array.Empty<int>();
        list.AddRangeFront(array);
        Validate(list, 3, 4, 5, 1, 2);
    }

    [Test]
    public void AddRangeFront_WithParamsArray()
    {
        var list = Make<int>(4);
        Validate(list);

        list.AddRangeFront(1, 2);
        Validate(list, 1, 2);

        list.AddRangeFront(3, 4, 5);
        Validate(list, 3, 4, 5, 1, 2);

        list.AddRangeFront();
        Validate(list, 3, 4, 5, 1, 2);
    }

    [Test]
    public void RemoveAtAndSwapFront_WithInvalidIndex_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Make<int>(null, 0).RemoveAtAndSwapFront(0));
        Should.Throw<ArgumentOutOfRangeException>(() => Make<int>(null, 5).RemoveAtAndSwapFront(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => Make<int>(null, 5).RemoveAtAndSwapFront(-100));
        Should.Throw<ArgumentOutOfRangeException>(() => Make<int>(null, 5).RemoveAtAndSwapFront(5));
        Should.Throw<ArgumentOutOfRangeException>(() => Make<int>(null, 5).RemoveAtAndSwapFront(6));
        Should.Throw<ArgumentOutOfRangeException>(() => Make<int>(null, 5).RemoveAtAndSwapFront(100));
    }

    [TestCase(true), TestCase(false)]
    public void RemoveAtAndSwapFront(bool wrap)
    {
        var rlist = new OkDeList<int>(10);
        rlist.AddRangeFront(1, 2, 3);

        rlist.RemoveAtAndSwapFront(0);
        Validate(rlist, 2, 3);
        rlist.RemoveAtAndSwapFront(0);
        Validate(rlist, 3);
        rlist.RemoveAtAndSwapFront(0);
        Validate(rlist);

        var flist = Make(10, new[] { 1, 2, 3 }, wrap);
        flist.RemoveAtAndSwapFront(2);
        Validate(flist, 2, 1);
        flist.RemoveAtAndSwapFront(1);
        Validate(flist, 2);
        flist.RemoveAtAndSwapFront(0);
        Validate(flist);
    }

    [Test]
    public void DropFront_WithEmpty_Throws()
    {
        Should.Throw<InvalidOperationException>(() => Make<int>(null, 0).DropFront());
    }

    [TestCase(true), TestCase(false)]
    public void DropFront_WithNonEmpty_Removes(bool wrap)
    {
        var list = Make(10, new[] { 0, 1, 2, 3, 4, 5 }, wrap);
        list[0].ShouldBe(0);
        list.DropFront();
        list[0].ShouldBe(1);
        list.DropFront();
        list[0].ShouldBe(2);
        list.DropFront();
        list[0].ShouldBe(3);
        list.DropFront();
        list[0].ShouldBe(4);
        list.DropFront();
        list[0].ShouldBe(5);
        list.DropFront();
        list.IsEmpty.ShouldBeTrue();
    }

    [Test]
    public void PopFront_WithEmpty_Throws()
    {
        Should.Throw<InvalidOperationException>(() => Make<int>(null, 0).PopFront());
    }

    [TestCase(true), TestCase(false)]
    public void PopFront_WithNonEmpty_RemovesAndReturnsItem(bool wrap)
    {
        var list = Make(10, new[] { 0, 1, 2, 3, 4, 5 }, wrap);
        list[0].ShouldBe(0);
        list.PopFront().ShouldBe(0);
        list[0].ShouldBe(1);
        list.PopFront().ShouldBe(1);
        list[0].ShouldBe(2);
        list.PopFront().ShouldBe(2);
        list[0].ShouldBe(3);
        list.PopFront().ShouldBe(3);
        list[0].ShouldBe(4);
        list.PopFront().ShouldBe(4);
        list[0].ShouldBe(5);
        list.PopFront().ShouldBe(5);
        list.IsEmpty.ShouldBeTrue();
    }

    [Test]
    public void PopAndDropAndSwapFront_UseUnderlyingCountClear()
    {
        // these ensure that we're using underlying valuetype-sensitive clear

        var vlist = Make(10, new[] { 1, 2, 3, 4, 5, 6 });
        vlist.DropFront();
        vlist.PopFront().ShouldBe(2);
        vlist.RemoveAtAndSwapFront(1);
        Validate(vlist, 3, 5, 6 );
        // valuetypes should not have been cleared
        vlist.PrivateRefAt(7).ShouldBe(1);
        vlist.PrivateRefAt(8).ShouldBe(2);
        vlist.PrivateRefAt(9).ShouldBe(3);

        var rlist = Make(10, new[] { "a", "b", "c", "d", "e", "f" });

        rlist.PrivateRefAt(7).ShouldBe("a");
        rlist.DropFront();
        rlist.PrivateRefAt(7).ShouldBeNull();

        rlist.PrivateRefAt(8).ShouldBe("b");
        rlist.PopFront().ShouldBe("b");
        rlist.PrivateRefAt(8).ShouldBeNull();

        rlist.PrivateRefAt(9).ShouldBe("c");
        rlist.RemoveAtAndSwapFront(1);
        rlist.PrivateRefAt(9).ShouldBeNull();

        Validate(rlist, "c", "e", "f");
    }

    [TestCase(true), TestCase(false)]
    public void Rotate_WithModuloZero_DoesNothing(bool wrap)
    {
        var list = Make(5, new[] { 1, 2, 3 }, wrap);
        var saved = list.PrivateGetFields();

        list.Rotate(0);
        list.PrivateGetFields().ShouldBe(saved);

        list.Rotate(3);
        list.PrivateGetFields().ShouldBe(saved);

        list.Rotate(-3);
        list.PrivateGetFields().ShouldBe(saved);

        list.Rotate(9);
        list.PrivateGetFields().ShouldBe(saved);
    }

    [TestCaseSource(nameof(RotateCaseSource))]
    public void Rotate_WithFullArray((int count, int[] final) rotateCase)
    {
        var source = Enumerable.Range(1, rotateCase.final.Length).ToArray();

        for (var head = 0; head < rotateCase.final.Length; ++head)
        {
            var list = new OkDeList<int>(rotateCase.final.Length);
            list.PrivateSetHead(head);

            list.AddRange(source);
            list.ShouldBe(source);

            var saved = list.PrivateGetFields();
            var items = saved.items.ToArray();

            // ensure rotation on a full array doesn't do anything except move the head
            void Rotate(int rotate)
            {
                list.Rotate(rotate);
                var rotated = list.PrivateGetFields();

                rotated.items.ShouldBeSameAs(saved.items); // same reference
                rotated.items.ShouldBe(items); // no change to elements
                rotated.used.ShouldBe(saved.used);
            }

            Rotate(rotateCase.count);
            list.ShouldBe(rotateCase.final);

            Rotate(-rotateCase.count);
            list.ShouldBe(source);
        }
    }

    // head==4 && capacity==8 && rotateCase.count==2

    [TestCaseSource(nameof(RotateCaseSource))]
    public void Rotate_WithGapArray((int count, int[] final) rotateCase)
    {
        var source = Enumerable.Range(1, rotateCase.final.Length).ToArray();

        // combos with capacity and head are for various gap sizes and locations
        for (var capacity = rotateCase.final.Length + 1; capacity < rotateCase.final.Length + 5; ++capacity)
        {
            for (var head = 0; head < capacity; ++head)
            {
                var list = new OkDeList<int>(capacity);
                list.PrivateSetHead(head);

                list.AddRange(source);
                list.ShouldBe(source);

                list.Rotate(rotateCase.count);
                list.ShouldBe(rotateCase.final);

                list.Rotate(-rotateCase.count);
                list.ShouldBe(source);
            }
        }
    }

    static (int, int[])[] RotateCaseSource => new[]
    {
        (-4, new[] { 5, 1, 2, 3, 4 }), // 12345 rot -4 ->    51234
        (-3, new[] { 4, 5, 1, 2, 3 }), // 12345 rot -3 ->   45123
        (-2, new[] { 3, 4, 5, 1, 2 }), // 12345 rot -2 ->  34512
        (-1, new[] { 2, 3, 4, 5, 1 }), // 12345 rot -1 -> 23451
        ( 0, new[] { 1, 2, 3, 4, 5 }), // 12345 rot           12345 (identity)
        (+1, new[] { 5, 1, 2, 3, 4 }), // 12345 rot +1 ->    51234
        (+2, new[] { 4, 5, 1, 2, 3 }), // 12345 rot +2 ->   45123
        (+3, new[] { 3, 4, 5, 1, 2 }), // 12345 rot +3 ->  34512
        (+4, new[] { 2, 3, 4, 5, 1 }), // 12345 rot +4 -> 23451
    };
}
