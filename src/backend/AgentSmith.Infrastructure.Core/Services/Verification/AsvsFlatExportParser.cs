using System.Text.Json;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Core.Services.Verification;

/// <summary>
/// 2026-08-30-0ea8: reads the standard's flat export into entries, keeping each id,
/// level and text exactly as the export writes them. A missing field is an error rather
/// than a blank: an entry the product half-read is an entry it cannot cite.
/// </summary>
internal sealed class AsvsFlatExportParser
{
    private const string RequirementsArray = "requirements";
    private const string IdField = "req_id";
    private const string LevelField = "L";
    private const string TextField = "req_description";

    public IReadOnlyList<VerificationRequirement> Parse(Stream export)
    {
        using var document = JsonDocument.Parse(export);
        if (!document.RootElement.TryGetProperty(RequirementsArray, out var requirements)
            || requirements.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                $"The verification export has no '{RequirementsArray}' array — "
                + "it is not the flat export of the pinned release.");
        return [.. requirements.EnumerateArray().Select(Read)];
    }

    private static VerificationRequirement Read(JsonElement entry) =>
        new(Field(entry, IdField), Field(entry, LevelField), Field(entry, TextField));

    private static string Field(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new InvalidOperationException(
                $"A verification export entry carries no string '{name}' field.");
}
