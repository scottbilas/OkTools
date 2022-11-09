using System.Reflection;
using Spreads.Buffers;

namespace OkTools.Unity.AssetDb;

[AttributeUsage(AttributeTargets.Method)]
public class AssetLmdbTableAttribute : Attribute
{
    readonly string _tableName, _csvFields;

    public AssetLmdbTableAttribute(string tableName, string csvFields)
    {
        _tableName = tableName;
        _csvFields = csvFields;
    }

    public bool UniqueKeys { get; set; }

    public static TableDumpSpec[] CreateTableDumpSpecs(Type type)
    {
        var expected = new[] { typeof(DumpContext), typeof(DirectBuffer), typeof(DirectBuffer) };

        return type
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Select(m =>
            {
                var attr = m.GetCustomAttribute<AssetLmdbTableAttribute>();
                if (attr == null)
                    return null;

                if (m.ReturnType != typeof(void) || !m.GetParameters().Select(p => p.ParameterType).SequenceEqual(expected))
                    throw new InvalidOperationException($"Method {m.DeclaringType}.{m.Name} is not `void {m.Name}(DumpContext, DirectBuffer, DirectBuffer)`");

                return new TableDumpSpec(
                    attr._tableName, attr._csvFields, attr.UniqueKeys,
                    (c, k, v) => m.Invoke(null, new object[] {c, k, v}));
            })
            .Where(s => s != null)
            .Select(m => m!)
            .ToArray();
    }
}
