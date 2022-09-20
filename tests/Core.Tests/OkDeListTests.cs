using System.Reflection;

// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo

partial class OkDeListTests
{
    static void Validate<T>(OkDeList<T> list, params T[] contents)
    {
        var head = list.GetType().GetField("_head", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(list);

        var rolist = (IReadOnlyList<T>)list;
        var segs = list.AsArraySegments;
        var memories = list.AsMemories;
        var spans = list.AsSpans;

        list.Count.ShouldBe(contents.Length);
        rolist.Count.ShouldBe(contents.Length);
        segs.seg0.Offset.ShouldBe(head);
        if (segs.seg1.Array != null)
            segs.seg1.Offset.ShouldBe(0);
        (segs.seg0.Count + segs.seg1.Count).ShouldBe(contents.Length);
        (memories.mem0.Length + memories.mem1.Length).ShouldBe(contents.Length);
        (spans.Span0.Length + spans.Span1.Length).ShouldBe(contents.Length);

        for (var i = 0; i < contents.Length; ++i)
        {
            list[i].ShouldBe(contents[i]);
            rolist[i].ShouldBe(contents[i]);

            if (i < segs.seg0.Count)
            {
                segs.seg0[i].ShouldBe(contents[i]);
                memories.mem0.Span[i].ShouldBe(contents[i]);
                spans.Span0[i].ShouldBe(contents[i]);
            }
            else
            {
                segs.seg1[i - segs.seg0.Count].ShouldBe(contents[i]);
                memories.mem1.Span[i - memories.mem0.Length].ShouldBe(contents[i]);
                spans.Span1[i - spans.Span0.Length].ShouldBe(contents[i]);
            }
        }
    }

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
            list.GetType().GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(list, items);
            list.GetType().GetField("_head", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(list, head);
            list.GetType().GetField("_used", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(list, used);
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
}
