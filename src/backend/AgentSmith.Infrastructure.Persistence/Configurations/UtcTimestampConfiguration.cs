using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// 2026-10-28-ebb0: PostgreSQL stores an instant, not an offset — Npgsql refuses to write a
/// DateTimeOffset whose offset is not zero into timestamptz, and the code writes local offsets
/// (an archive copied from SQLite carries +02:00 as text, and every DateTimeOffset.Now does too).
/// So on that provider every DateTimeOffset column converts to UTC on the way in: the same
/// instant, read back at offset zero. The other providers keep the offset they were given, and
/// the provider is asked by name so no other model snapshot changes.
/// <para>
/// Applied LAST, over every entity the model holds, so a column added later is covered without
/// anyone remembering this file.
/// </para>
/// </summary>
public sealed class UtcTimestampConfiguration(string? providerName)
{
    private const string NpgsqlProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";

    private static readonly ValueConverter<DateTimeOffset, DateTimeOffset> ToUtc =
        new(value => value.ToUniversalTime(), value => value);

    public void Apply(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        if (providerName != NpgsqlProvider) return;
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
            if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                property.SetValueConverter(ToUtc);
    }
}
