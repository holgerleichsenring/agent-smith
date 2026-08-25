using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentSmith.Sandbox.Wire;

/// <summary>
/// 2026-08-25-0d01: reads a wire enum and answers a caller-chosen fallback for a value it
/// does not know, instead of throwing.
/// <para>
/// The stock <c>JsonStringEnumConverter</c> throws on an unknown string. That throw came
/// out of the agent's blocking read, escaped a loop catching only Redis faults, exited the
/// container, and reached the operator as "sandbox vanished" — a newer server sending an
/// older agent one message kind it had never heard of looked exactly like a dead pod. A
/// value this build cannot name has to survive deserialisation, because every judgement
/// about a protocol difference happens AFTER the message is in hand.
/// </para>
/// </summary>
public sealed class TolerantEnumConverter<TEnum>(TEnum fallback) : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private static readonly JsonNamingPolicy Naming = JsonNamingPolicy.CamelCase;

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => FromName(reader.GetString()),
            JsonTokenType.Number when reader.TryGetInt32(out var number) => FromNumber(number),
            _ => fallback
        };

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
        writer.WriteStringValue(Naming.ConvertName(value.ToString()));

    private TEnum FromName(string? name) =>
        Enum.TryParse<TEnum>(name, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : fallback;

    private TEnum FromNumber(int number) =>
        Enum.IsDefined(typeof(TEnum), number) ? (TEnum)Enum.ToObject(typeof(TEnum), number) : fallback;
}
