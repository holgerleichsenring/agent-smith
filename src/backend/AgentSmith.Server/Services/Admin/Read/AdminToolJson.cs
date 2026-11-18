using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentSmith.Server.Services.Admin.Read;

/// <summary>2026-11-17-f3a8b: what an admin read hands the model — compact camelCase JSON, nulls left out.</summary>
internal static class AdminToolJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    internal static string Render(object? value) => JsonSerializer.Serialize(value, Options);
}
