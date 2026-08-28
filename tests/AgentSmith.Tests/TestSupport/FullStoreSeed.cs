using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-08-28-2af6: fills EVERY table of the store with representative rows, driven by the
/// model itself, so a table added next year is seeded without anyone remembering to.
/// <para>
/// Two rows per table: one with every column populated, one with every nullable column
/// null. Plus the two shapes that broke a hand transfer between the providers — a row whose
/// text carries a dollar-parenthesis sequence a client tool would substitute, and a row
/// whose body is tens of thousands of characters.
/// </para>
/// </summary>
internal sealed class FullStoreSeed
{
    internal const string ShellSubstitutionText =
        "budget $(SELECT name FROM sys.tables) and ${HOME} plus $(echo hi) — 'single' \"double\" "
        + "\\backslash\ttab\nsecond line; drop table Runs; -- ünïcode 日本語 \u0001";

    internal const long ShellSubstitutionArtifactId = 900_001;

    internal const long VeryLongArtifactId = 900_002;

    internal static readonly DateTimeOffset SeededAt =
        new(2021, 3, 4, 5, 6, 7, TimeSpan.FromHours(2));

    internal static string VeryLongBody { get; } =
        string.Concat(Enumerable.Repeat("0123456789 the quick brown fox — $(x) ", 1_400));

    internal async Task SeedAsync(AgentSmithDbContext db)
    {
        using var stamping = db.SuspendAuditStamping();
        var rows = new List<object>();
        foreach (var type in db.Model.GetEntityTypes().Where(t => t.GetTableName() is not null))
        {
            rows.Add(Row(type, 1, nullNullables: false));
            rows.Add(Row(type, 2, nullNullables: true));
        }

        LinkConfigRefs(rows);
        db.AddRange(rows);
        db.AddRange(SpecialArtifacts());
        await db.SaveChangesAsync();
    }

    private static IEnumerable<object> SpecialArtifacts() =>
    [
        Artifact(ShellSubstitutionArtifactId, "shell-substitution", ShellSubstitutionText),
        Artifact(VeryLongArtifactId, "very-long-body", VeryLongBody),
    ];

    private static RunArtifact Artifact(long id, string kind, string content) => new()
    {
        Id = id,
        RunId = $"run-{kind}",
        Kind = kind,
        Content = content,
        CreatedAt = SeededAt,
        UpdatedAt = SeededAt.AddMinutes(1),
    };

    // The one enforced foreign key in the model: a config reference points at a config
    // entity by (EntityType, EntityId), so the seeded edges point at a seeded entity.
    private static void LinkConfigRefs(List<object> rows)
    {
        var entity = rows.OfType<ConfigEntity>().First();
        foreach (var reference in rows.OfType<ConfigRef>())
        {
            reference.ToType = entity.EntityType;
            reference.ToId = entity.EntityId;
        }
    }

    private static object Row(IEntityType type, int index, bool nullNullables)
    {
        var row = Activator.CreateInstance(type.ClrType)!;
        foreach (var property in type.GetProperties())
        {
            var value = nullNullables && property.IsNullable ? null : Value(property, index);
            property.PropertyInfo!.SetValue(row, value);
        }

        return row;
    }

    private static object Value(IProperty property, int index)
    {
        var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        if (clrType == typeof(string)) return Text(property, index);
        if (clrType == typeof(long)) return Number(property, index);
        if (clrType == typeof(int)) return (int)Number(property, index);
        if (clrType == typeof(short)) return (short)(Number(property, index) % 1000);
        if (clrType == typeof(bool)) return index % 2 == 1;
        if (clrType == typeof(decimal)) return 12.34m + index;
        if (clrType == typeof(double)) return 1.5 + index;
        if (clrType == typeof(DateTimeOffset)) return Instant(property, index);
        throw new NotSupportedException(
            $"The model gained a {clrType.Name} column ({property.Name}); the seed must cover it.");
    }

    private static string Text(IProperty property, int index)
    {
        var value = $"{property.DeclaringType.ShortName()}-{property.Name}-{index}";
        var max = property.GetMaxLength();
        return max is not null && value.Length > max ? value[..max.Value] : value;
    }

    // Keys are far from 1..n on purpose: a target that regenerated them would not match.
    private static long Number(IProperty property, int index) =>
        property.IsPrimaryKey()
            ? 1_000 + (index * 7)
            : (index * 100) + (NameHash(property.Name) % 97);

    private static DateTimeOffset Instant(IProperty property, int index) =>
        SeededAt.AddSeconds(index).AddTicks(NameHash(property.Name) % 9_999_999);

    private static int NameHash(string name) => name.Sum(c => c) * 31;
}
