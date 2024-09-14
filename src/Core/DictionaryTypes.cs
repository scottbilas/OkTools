using System.Collections;
using System.Diagnostics;
#if NET
using System.Diagnostics.CodeAnalysis;
#endif

namespace OkTools.Core;

[PublicAPI]
public class EmptyDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue> where TKey: notnull
{
    public static readonly IReadOnlyDictionary<TKey, TValue> Instance = new EmptyDictionary<TKey, TValue>();

    EmptyDictionary() {} // use `.Instance`

    [MustDisposeResource]
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => Enumerable.Empty<KeyValuePair<TKey, TValue>>().GetEnumerator();
    [MustDisposeResource]
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public int Count => 0;
    public bool ContainsKey(TKey key) => false;
#   if NET
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) { value = default; return false; }
#   else
    public bool TryGetValue(TKey key, out TValue value) { value = default!; return false; }
#   endif
    public TValue this[TKey key] => throw new KeyNotFoundException();
    public IEnumerable<TKey> Keys => [];
    public IEnumerable<TValue> Values => [];
}

[PublicAPI]
[DebuggerTypeProxy(typeof(DefaultDictionary<,>.DebugView))]
[DebuggerDisplay("Count = {Count}")]
public sealed class DefaultDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue> where TKey : notnull
{
    readonly IDictionary<TKey, TValue> _dict;
    readonly Func<TKey, TValue> _getDefValue;

    public DefaultDictionary(Func<TKey, TValue> getDefValue)
        { _dict = new Dictionary<TKey, TValue>(); _getDefValue = getDefValue; }
    public DefaultDictionary(Func<TKey, TValue> getDefValue, int capacity)
        { _dict = new Dictionary<TKey, TValue>(capacity); _getDefValue = getDefValue; }
    public DefaultDictionary(Func<TKey, TValue> getDefValue, IEqualityComparer<TKey>? comparer)
        { _dict = new Dictionary<TKey, TValue>(comparer); _getDefValue = getDefValue; }
    public DefaultDictionary(Func<TKey, TValue> getDefValue, int capacity, IEqualityComparer<TKey>? comparer)
        { _dict = new Dictionary<TKey, TValue>(capacity, comparer); _getDefValue = getDefValue; }
    public DefaultDictionary(Func<TKey, TValue> getDefValue, IDictionary<TKey, TValue> dictionary)
        { _dict = new Dictionary<TKey, TValue>(dictionary); _getDefValue = getDefValue; }
    public DefaultDictionary(Func<TKey, TValue> getDefValue, IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey>? comparer)
        { _dict = new Dictionary<TKey, TValue>(dictionary, comparer); _getDefValue = getDefValue; }
    public DefaultDictionary(Func<TKey, TValue> getDefValue, IEnumerable<KeyValuePair<TKey, TValue>> collection)
        { _dict = new Dictionary<TKey, TValue>(collection); _getDefValue = getDefValue; }
    public DefaultDictionary(Func<TKey, TValue> getDefValue, IEnumerable<KeyValuePair<TKey, TValue>> collection, IEqualityComparer<TKey>? comparer)
        { _dict = new Dictionary<TKey, TValue>(collection, comparer); _getDefValue = getDefValue; }

    public DefaultDictionary(Func<TValue> getDefValue)
        : this(_ => getDefValue()) {}
    public DefaultDictionary(Func<TValue> getDefValue, int capacity)
        : this(_ => getDefValue(), capacity) {}
    public DefaultDictionary(Func<TValue> getDefValue, IEqualityComparer<TKey>? comparer)
        : this(_ => getDefValue(), comparer) {}
    public DefaultDictionary(Func<TValue> getDefValue, int capacity, IEqualityComparer<TKey>? comparer)
        : this(_ => getDefValue(), capacity, comparer) {}
    public DefaultDictionary(Func<TValue> getDefValue, IDictionary<TKey, TValue> dictionary)
        : this(_ => getDefValue(), dictionary) {}
    public DefaultDictionary(Func<TValue> getDefValue, IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey>? comparer)
        : this(_ => getDefValue(), dictionary, comparer) {}
    public DefaultDictionary(Func<TValue> getDefValue, IEnumerable<KeyValuePair<TKey, TValue>> collection)
        : this(_ => getDefValue(), collection) {}
    public DefaultDictionary(Func<TValue> getDefValue, IEnumerable<KeyValuePair<TKey, TValue>> collection, IEqualityComparer<TKey>? comparer)
        : this(_ => getDefValue(), collection, comparer) {}

    public DefaultDictionary(TValue defValue)
        : this(_ => defValue) {}
    public DefaultDictionary(TValue defValue, int capacity)
        : this(_ => defValue, capacity) {}
    public DefaultDictionary(TValue defValue, IEqualityComparer<TKey>? comparer)
        : this(_ => defValue, comparer) {}
    public DefaultDictionary(TValue defValue, int capacity, IEqualityComparer<TKey>? comparer)
        : this(_ => defValue, capacity, comparer) {}
    public DefaultDictionary(TValue defValue, IDictionary<TKey, TValue> dictionary)
        : this(_ => defValue, dictionary) {}
    public DefaultDictionary(TValue defValue, IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey>? comparer)
        : this(_ => defValue, dictionary, comparer) {}
    public DefaultDictionary(TValue defValue, IEnumerable<KeyValuePair<TKey, TValue>> collection)
        : this(_ => defValue, collection) {}
    public DefaultDictionary(TValue defValue, IEnumerable<KeyValuePair<TKey, TValue>> collection, IEqualityComparer<TKey>? comparer)
        : this(_ => defValue, collection, comparer) {}

    public int Count => _dict.Count;
    public bool IsReadOnly => _dict.IsReadOnly;

    public ICollection<TKey> Keys => _dict.Keys;
    public ICollection<TValue> Values => _dict.Values;

    [MustDisposeResource]
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dict.GetEnumerator();

#   if NET
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => _dict.TryGetValue(key, out value);
#   else
    public bool TryGetValue(TKey key, out TValue value) => _dict.TryGetValue(key, out value);
#   endif
    public bool Contains(KeyValuePair<TKey, TValue> item) => _dict.Contains(item);
    public bool ContainsKey(TKey key) => _dict.ContainsKey(key);

    public void Add(KeyValuePair<TKey, TValue> item) => _dict.Add(item);
    public void Add(TKey key, TValue value) => _dict.Add(key, value);
    public void Add(TKey key) => _dict.Add(key, _getDefValue(key));

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => _dict.CopyTo(array, arrayIndex);

    public bool Remove(KeyValuePair<TKey, TValue> item) => _dict.Remove(item);
    public bool Remove(TKey key) => _dict.Remove(key);

    public void Clear() => _dict.Clear();

    public TValue this[TKey key]
    {
        [CollectionAccess(CollectionAccessType.UpdatedContent)]
        get => _dict.TryGetValue(key, out var value) ? value : _dict[key] = _getDefValue(key);
        set => _dict[key] = value;
    }

    [MustDisposeResource]
    IEnumerator IEnumerable.GetEnumerator() => _dict.GetEnumerator();
    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;
    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;

    sealed class DebugView
    {
        readonly IDictionary<TKey, TValue> _dict;

        public DebugView(IDictionary<TKey, TValue> dictionary) =>
            _dict = dictionary ?? throw new ArgumentNullException(nameof(dictionary));

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden), UsedImplicitly]
        public KeyValuePair<TKey, TValue>[] Items
        {
            get
            {
                var items = new KeyValuePair<TKey, TValue>[_dict.Count];
                _dict.CopyTo(items, 0);
                return items;
            }
        }
    }
}
