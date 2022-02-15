using System.Text.Json;
using System.Text.Json.Serialization;
using DocoptNet;

class ToStringConverter<T> : JsonConverter<T>
{
    public bool ExactTypeMatch { get; init; } = true;

    public override bool CanConvert(Type typeToConvert) =>
        ExactTypeMatch
            ? typeof(T) == typeToConvert
            : typeof(T).IsAssignableFrom(typeToConvert);

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException();
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value?.ToString());
}

static class Extensions
{
    public static IEnumerable<string> AsStrings(this ValueObject @this) =>
        @this.AsList.Cast<string>();
}
