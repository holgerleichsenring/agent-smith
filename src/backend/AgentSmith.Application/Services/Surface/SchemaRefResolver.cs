using System.Text.Json;

namespace AgentSmith.Application.Services.Surface;

/// <summary>
/// 2026-08-30-c6ec: follows a served description's internal <c>$ref</c> to the component it
/// names, so an operation whose body is a reference still states the properties it accepts.
/// Local references only — a reference out of the document is a fact this run does not hold.
/// </summary>
internal sealed class SchemaRefResolver(JsonElement? root)
{
    public static SchemaRefResolver For(JsonElement? root) => new(root);

    public bool TryResolve(string? reference, out JsonElement target)
    {
        target = default;
        if (root is not { } document || reference is null || !reference.StartsWith("#/", StringComparison.Ordinal))
            return false;

        var current = document;
        foreach (var segment in reference[2..].Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = segment.Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out var next))
                return false;
            current = next;
        }

        target = current;
        return true;
    }
}
