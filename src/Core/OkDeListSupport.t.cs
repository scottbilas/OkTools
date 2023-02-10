using System.Diagnostics;
using System.Reflection;

// ReSharper disable IdentifierTypo

static class OkDeListExtensions
{
    static class Cache<T>
    {
        public static readonly Action<OkDeList<T>, T[]> SetItems;
        public static readonly Func<OkDeList<T>, T[]> GetItems;
        public static readonly Action<OkDeList<T>, int> SetHead;
        public static readonly Func<OkDeList<T>, int> GetHead;
        public static readonly Action<OkDeList<T>, int> SetUsed;
        public static readonly Func<OkDeList<T>, int> GetUsed;

        static Cache()
        {
            var type = typeof(OkDeList<T>);

            var itemsField = type.GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic);
            Debug.Assert(itemsField != null);
            SetItems = (list, items) => itemsField.SetValue(list, items);
            GetItems = list => (T[])itemsField.GetValue(list)!;

            var headField = type.GetField("_head", BindingFlags.Instance | BindingFlags.NonPublic);
            Debug.Assert(headField != null);
            SetHead = (list, head) => headField.SetValue(list, head);
            GetHead = list => (int)headField.GetValue(list)!;

            var usedField = type.GetField("_used", BindingFlags.Instance | BindingFlags.NonPublic);
            Debug.Assert(usedField != null);
            SetUsed = (list, used) => usedField.SetValue(list, used);
            GetUsed = list => (int)usedField.GetValue(list)!;
        }
    }

    public static T[] PrivateGetItems<T>(this OkDeList<T> @this) => Cache<T>.GetItems(@this);
    public static void PrivateSetItems<T>(this OkDeList<T> @this, T[] items) => Cache<T>.SetItems(@this, items);
    public static int PrivateGetHead<T>(this OkDeList<T> @this) => Cache<T>.GetHead(@this);
    public static void PrivateSetHead<T>(this OkDeList<T> @this, int head) => Cache<T>.SetHead(@this, head);
    public static int PrivateGetUsed<T>(this OkDeList<T> @this) => Cache<T>.GetUsed(@this);
    public static void PrivateSetUsed<T>(this OkDeList<T> @this, int used) => Cache<T>.SetUsed(@this, used);

    public static (T[] items, int head, int used) PrivateGetFields<T>(this OkDeList<T> @this) =>
        (@this.PrivateGetItems(), @this.PrivateGetHead(), @this.PrivateGetUsed());
}

partial class OkDeListTests
{
    static OkDeList<T> Add<T>(OkDeList<T> list, T[]? items, bool wrap)
    {
        if (items == null)
            return list;

        if (wrap)
        {
            list.AddRange(items[(items.Length/2)..]); // add about half normally
            list.AddRangeFront(items[..^list.Count]);  // add the remainder at the front
            list.ToArray().ShouldBe(items);
        }
        else
            list.AddRange(items);

        return list;
    }

    static OkDeList<T> Make<T>(int? capacity, int count, T[]? items = null, bool wrap = true) =>
        Add(new OkDeList<T>(capacity, count), items, wrap);
    static OkDeList<T> Make<T>(int capacity, T[]? items = null, bool wrap = true) =>
        Add(new OkDeList<T>(capacity), items, wrap);

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

    [Test]
    public void PrivateGetFields()
    {
        var list = Make(5, new[] { 1, 2 }, false);
        var saved = list.PrivateGetFields();

        list.Add(3);
        var check0 = list.PrivateGetFields();
        check0.ShouldNotBe(saved);
        check0.items.ShouldBe(saved.items);
        check0.head.ShouldBe(saved.head);
        check0.used.ShouldNotBe(saved.used);

        list.DropBack();
        list.PrivateGetFields().ShouldBe(saved);

        list.Add(4);
        list.DropFront();
        var check1 = list.PrivateGetFields();
        check1.ShouldNotBe(saved);
        check1.items.ShouldBe(saved.items);
        check1.head.ShouldNotBe(saved.head);
        check1.used.ShouldBe(saved.used);

        list.DropBack();
        list.AddFront(1);
        list.PrivateGetFields().ShouldBe(saved);

        ++list.Capacity;
        var check2 = list.PrivateGetFields();
        check2.ShouldNotBe(saved);
        check2.items.ShouldNotBe(saved.items);
        check2.head.ShouldBe(saved.head);
        check2.used.ShouldBe(saved.used);
    }
}
