using System.Text.Json;

namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-11-28-db88a: the ONE wire shape of a ticket's working spec set — the string the TicketSeries row stores. One
/// configuration for writing and reading, like <see cref="SpecApprovalJson"/>, so a set this system stored is always one
/// it reads back.
/// </summary>
public static class SpecSetJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public static string Write(SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return JsonSerializer.Serialize(set, Options);
    }

    /// <summary>The set the text carries, or null when it is absent, unreadable or hollow (no key, no specs list, no
    /// revision) — a set every reader would dereference into an exception is no set.</summary>
    public static SpecSet? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var set = JsonSerializer.Deserialize<SpecSet>(json!, Options);
            return set is { Key.Length: > 0, Specs: not null, Revisions.Count: > 0, Accounting: not null } ? set : null;
        }
        catch (JsonException) { return null; }
        catch (NotSupportedException) { return null; }
    }
}
