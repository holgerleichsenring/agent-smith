using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Deserializes JSON <c>object</c>-typed values into plain CLR types
/// (string / double / bool / null / List&lt;object?&gt; / Dictionary&lt;string, object?&gt;)
/// instead of leaving them as <see cref="JsonElement"/>.
///
/// context.yaml's pass-through sections (arch / quality / behavior) are typed as
/// <c>IDictionary&lt;string, object?&gt;</c>. Without this converter their values
/// land as <see cref="JsonElement"/>, and the YAML serializer — which reflects an
/// object's public properties — emits <c>value_kind: String</c> instead of the
/// actual scalar/list. This converter closes that round-trip so the written
/// context.yaml preserves the real arch/quality content.
/// </summary>
public sealed class InferredTypeJsonConverter : JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Null => null,
            JsonTokenType.Number => reader.TryGetInt64(out var l) ? l : reader.GetDouble(),
            JsonTokenType.StartArray => ReadArray(ref reader, options),
            JsonTokenType.StartObject => ReadObject(ref reader, options),
            _ => throw new JsonException($"Unsupported token {reader.TokenType}"),
        };

    private List<object?> ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var list = new List<object?>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            list.Add(Read(ref reader, typeof(object), options));
        return list;
    }

    private Dictionary<string, object?> ReadObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var key = reader.GetString()!;
            reader.Read();
            map[key] = Read(ref reader, typeof(object), options);
        }
        return map;
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
}
