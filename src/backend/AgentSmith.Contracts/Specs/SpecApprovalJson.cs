using System.Text.Json;

namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-09-17-0e79a: the ONE wire shape of an approved record — the string it rides as on a
/// run's initial context and the string the relational row stores. One configuration for both
/// halves, so a record this system writes is always one it can read back.
/// <para>
/// It rides as a plain JSON STRING for the reason the resume payload does: the Redis job queue
/// re-materializes request context values as <c>JsonElement</c>, and a string survives that
/// round-trip with its value semantics intact.
/// </para>
/// </summary>
public static class SpecApprovalJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public static string Write(SpecApprovalRecord record) =>
        JsonSerializer.Serialize(record, Options);

    /// <summary>
    /// The record the text carries, or null when it is absent, unreadable or HOLLOW. Valid JSON
    /// that names a key and nothing else deserializes to a record whose Set and Repositories are
    /// null, which every reader then dereferences; "not a record" is the honest answer, and it
    /// leaves a filed ticket failing loudly rather than throwing mid-step.
    /// </summary>
    public static SpecApprovalRecord? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var record = JsonSerializer.Deserialize<SpecApprovalRecord>(json!, Options);
            return record is { Set: not null, Repositories: not null, Key.Length: > 0 } ? record : null;
        }
        catch (JsonException) { return null; }
        catch (NotSupportedException) { return null; }
    }
}
