using System.Text.Json;
using System.Text.Json.Serialization;

namespace Protegey.Sdk;

/// <summary>
/// System.Text.Json deserializes an untyped <c>object</c> as <see cref="JsonElement"/> by
/// default, which is awkward for callers (every value needs <c>.GetString()</c>/<c>.GetInt32()</c>
/// instead of a plain cast). This converter recursively resolves each value to its native .NET
/// type instead — <c>string</c>, <c>double</c>, <c>bool</c>, <c>null</c>,
/// <c>List&lt;object?&gt;</c>, or <c>Dictionary&lt;string, object?&gt;</c> — so
/// <c>result["decision"]</c> behaves the same way Gson's <c>Map&lt;String, Object&gt;</c> does for
/// the Java SDK, or a PHP associative array does for the PHP one.
/// </summary>
internal sealed class ObjectToInferredTypesConverter : JsonConverter<object?>
{
    // Instance override just delegates to a static implementation — ReadArray/ReadObject need to
    // recurse back into "read one value" too, and a static method can't call an instance one.
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ReadValue(ref reader, options);

    private static object? ReadValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number => reader.TryGetInt64(out var l) ? l : reader.GetDouble(),
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Null => null,
            JsonTokenType.StartArray => ReadArray(ref reader, options),
            JsonTokenType.StartObject => ReadObject(ref reader, options),
            _ => JsonDocument.ParseValue(ref reader).RootElement.Clone(),
        };
    }

    private static List<object?> ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var list = new List<object?>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            list.Add(ReadValue(ref reader, options));
        }
        return list;
    }

    private static Dictionary<string, object?> ReadObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var dict = new Dictionary<string, object?>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var key = reader.GetString()!;
            reader.Read();
            dict[key] = ReadValue(ref reader, options);
        }
        return dict;
    }

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value?.GetType() ?? typeof(object));
    }
}
