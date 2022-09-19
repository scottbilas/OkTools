partial class OkListTests
{
    static void Validate<T>(OkList<T> list, params T[] contents)
    {
        var rolist = (IReadOnlyList<T>)list;
        var array = list.AsArraySegment;
        var memory = list.AsMemory;
        var span = list.AsSpan;

        list.Count.ShouldBe(contents.Length);
        rolist.Count.ShouldBe(contents.Length);
        array.Offset.ShouldBe(0);
        array.Count.ShouldBe(contents.Length);
        memory.Length.ShouldBe(contents.Length);
        span.Length.ShouldBe(contents.Length);

        for (var i = 0; i < contents.Length; ++i)
        {
            list[i].ShouldBe(contents[i]);
            rolist[i].ShouldBe(contents[i]);
            array[i].ShouldBe(contents[i]);
            memory.Span[i].ShouldBe(contents[i]);
            span[i].ShouldBe(contents[i]);
        }
    }

}
