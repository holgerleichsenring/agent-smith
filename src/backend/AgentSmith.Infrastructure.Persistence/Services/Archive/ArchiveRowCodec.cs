using System.Text.Json;
using AgentSmith.Domain.Exceptions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services.Archive;

/// <summary>
/// 2026-08-28-2af6: one row to one JSON line and back, driven by the model's own property
/// list. Values are written as JSON values — a number stays a number, a timestamp keeps
/// its offset, a string keeps every character it had — so nothing depends on a dialect's
/// literal syntax and nothing is escaped twice.
/// </summary>
public sealed class ArchiveRowCodec
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public string Encode(IEntityType type, object row)
    {
        ArgumentNullException.ThrowIfNull(type);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in type.GetProperties())
            values[property.Name] = Accessor(type, property).GetValue(row);
        return JsonSerializer.Serialize(values, Json);
    }

    public object Decode(IEntityType type, string line)
    {
        ArgumentNullException.ThrowIfNull(type);
        var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line, Json)
            ?? throw new DataArchiveException($"A row of '{type.GetTableName()}' is not a JSON object.");
        var row = Activator.CreateInstance(type.ClrType)
            ?? throw new DataArchiveException($"'{type.ClrType.Name}' cannot be constructed.");
        foreach (var property in type.GetProperties())
            Accessor(type, property).SetValue(row, Value(type, property, values));
        return row;
    }

    private static object? Value(
        IEntityType type, IProperty property, Dictionary<string, JsonElement> values)
    {
        if (!values.TryGetValue(property.Name, out var element))
            throw new DataArchiveException(
                $"The archive's '{type.GetTableName()}' rows carry no '{property.Name}'.");
        return element.ValueKind == JsonValueKind.Null
            ? null
            : element.Deserialize(property.ClrType, Json);
    }

    // A shadow property has no CLR member to read or write, so the archive would silently
    // drop the column. The model has none; if one is ever added, this says so.
    private static System.Reflection.PropertyInfo Accessor(IEntityType type, IProperty property) =>
        property.PropertyInfo
        ?? throw new DataArchiveException(
            $"'{type.GetTableName()}.{property.Name}' is a shadow property, which an archive cannot carry.");
}
