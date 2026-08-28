using Microsoft.EntityFrameworkCore.Metadata;

namespace AgentSmith.Infrastructure.Persistence.Services.Archive;

/// <summary>
/// 2026-08-28-2af6: the database-generated integer key of a table, or nothing. Twenty of
/// the twenty-two tables have one; a run keys on its own sortable id and an observed
/// caller on its subject, and neither needs anything switched on to take a copied key.
/// This is the question both the identity-insert switch and the generator advance ask.
/// </summary>
public sealed class GeneratedKeyProperty
{
    public IProperty? Of(IEntityType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var key = type.FindPrimaryKey();
        if (key is null || key.Properties.Count != 1) return null;
        var property = key.Properties[0];
        return property.ValueGenerated == ValueGenerated.OnAdd && IsInteger(property.ClrType)
            ? property
            : null;
    }

    private static bool IsInteger(Type clrType) =>
        clrType == typeof(long) || clrType == typeof(int) || clrType == typeof(short);
}
