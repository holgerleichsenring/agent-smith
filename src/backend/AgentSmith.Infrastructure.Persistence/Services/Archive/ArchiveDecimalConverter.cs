using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentSmith.Infrastructure.Persistence.Services.Archive;

/// <summary>
/// Writes a decimal without the trailing zeros its column's scale supplies, so the same
/// data yields the same archive whichever provider it was read from.
/// <para>
/// A provider hands a decimal back at its column's scale: SQL Server's decimal(18,10)
/// returns 13.3400000000 where SQLite's text column returns 13.34. The VALUES are equal
/// and either restores correctly — but the archive is a file people diff, and two exports
/// of one database differing only in trailing zeros is noise that hides a real difference.
/// </para>
/// </summary>
internal sealed class ArchiveDecimalConverter : JsonConverter<decimal>
{
    public override decimal Read(
        ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
        reader.GetDecimal();

    // Dividing by one drops the trailing zeros without touching the value: the result of
    // a decimal division carries the smaller scale of the two operands' representations.
    public override void Write(
        Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteRawValue((value / 1.000000000000000000000000000000000m)
            .ToString(System.Globalization.CultureInfo.InvariantCulture));
}
