using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0416: the one JSON shape of the worker protocol — snake_case names, nulls omitted,
/// indented so a human worker can read the request it is answering. Instance, not a
/// static: the format is a dependency of the renderer and the parser, and both must be
/// unable to disagree about it.
/// </summary>
public sealed class WorkerJsonFormat
{
    public JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public JsonElement ToElement<T>(T value) => JsonSerializer.SerializeToElement(value, Options);
}
