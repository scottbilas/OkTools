using System.Dynamic;

namespace OkTools.Core;

[PublicAPI]
public static class Expando
{
    public static dynamic From(object obj)
    {
        dynamic expando = new ExpandoObject();
        Add(expando, obj);
        return expando;
    }

    public static void Add(dynamic dst, object src)
    {
        var expando = (IDictionary<string, object?>)dst;

        foreach (var property in src.GetType().GetProperties())
            expando.Add(property.Name, property.GetValue(src));
    }
}
