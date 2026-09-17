using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042el: reads an optional enum stored by name and answers null for anything it cannot
/// name — an unknown word, a number, or a value of another shape — instead of throwing. A stored
/// transcript is read whole, so one value this build does not know would otherwise make the
/// entire conversation unreadable. Only the names are accepted: a number or a comma list would
/// parse into a value nobody wrote.
/// </summary>
public sealed class TolerantNullableEnumConverter<TEnum> : JsonConverter<TEnum?>
    where TEnum : struct, Enum
{
    public override TEnum? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            reader.Skip();
            return null;
        }
        var name = reader.GetString();
        var known = Enum.GetNames<TEnum>()
            .FirstOrDefault(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));
        return known is null ? null : Enum.Parse<TEnum>(known);
    }

    public override void Write(Utf8JsonWriter writer, TEnum? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(JsonNamingPolicy.CamelCase.ConvertName(value.Value.ToString()));
    }
}
