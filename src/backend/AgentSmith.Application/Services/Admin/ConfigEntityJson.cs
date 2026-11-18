using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentSmith.Application.Services.Admin;

/// <summary>2026-11-17-f3a8c: the JSON an admin draft reads and writes an entity in — the web's camelCase, enums as names.</summary>
public static class ConfigEntityJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The entity as a draft shows it; null for one the store does not hold.</summary>
    public static string? Render(object? entity) => entity is null ? null : JsonSerializer.Serialize(entity, entity.GetType(), Options);
}
