using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0344b: server-computed state of the five run-story beats, served on the run
/// list AND detail snapshots. Values are <see cref="BeatStates"/> strings. Null
/// on the snapshot means the run's stored data predates the typed step records
/// and cannot be mapped — the client renders NO storybar rather than guessing.
/// </summary>
public sealed record RunBeatsView(
    string Ticket,
    string Plan,
    string Building,
    string Verify,
    string Outcome);

/// <summary>p0344b: the beat-state vocabulary of <see cref="RunBeatsView"/>.</summary>
public static class BeatStates
{
    public const string Done = "done";
    public const string Active = "active";
    public const string Pending = "pending";
    public const string Failed = "failed";
    public const string Skipped = "skipped";
}

/// <summary>
/// p0344b: one persisted progress-ledger item as served on the run detail —
/// the p0341 ProgressLedger snapshot taken at run end. Status vocabulary:
/// "pending" | "in_progress" | "done".
/// <para>p0356: Note is persisted too — mid-run flushes make the stored ledger
/// a RESUME seed, and the note is exactly the working state (decisions, batch
/// counts) a resumed run needs. Null notes are omitted from the wire JSON, so
/// pre-p0356 payloads deserialize unchanged.</para>
/// </summary>
public sealed record ProgressLedgerItemView(
    string Id,
    string Activity,
    string Status,
    string? Target,
    string? Note = null);

/// <summary>
/// p0374a: one ledger TRANSITION as it rides the run trail — the record the
/// overwritten ledger snapshot cannot keep. <see cref="From"/> is null when the
/// entry was added, <see cref="To"/> null when it was dropped; a refused rewrite
/// carries From == To == "done" and a cause naming the refusal. Status vocabulary
/// matches <see cref="ProgressLedgerItemView"/>: "pending" | "in_progress" | "done".
/// Cause vocabulary: see <see cref="LedgerTransitionCauses"/>.
/// </summary>
public sealed record LedgerTransitionView(
    string EntryId,
    string Activity,
    string? From,
    string? To,
    string Cause,
    int Pass);

/// <summary>p0374a: the wire vocabulary of <see cref="LedgerTransitionView.Cause"/>.</summary>
public static class LedgerTransitionCauses
{
    public const string Added = "added";
    public const string ModelUpdate = "model_update";
    public const string ExplicitReopen = "explicit_reopen";
    public const string RegressionRefused = "regression_refused";
    public const string OmissionRefused = "omission_refused";
    public const string Dropped = "dropped";
}

/// <summary>
/// p0344b: the run's acceptance contract as served on the run detail — the
/// ratified criteria (p0328) paired with the master's per-criterion dispositions
/// (p0340), snapshotted at run end. Criterion status vocabulary: "met" | "unmet"
/// | "not_applicable" | "unproven" ("unproven" = the criterion was ratified but
/// the master reported no disposition for it). Outcome/RatifiedBy carry the
/// ratification facts (verbatim / edited / unratified, and who ratified).
/// </summary>
public sealed record AcceptanceView(
    IReadOnlyList<AcceptanceCriterionView> Criteria,
    string Outcome,
    string RatifiedBy);

public sealed record AcceptanceCriterionView(
    string Text,
    string Status,
    string? Reason);

/// <summary>Criterion-status vocabulary of <see cref="AcceptanceCriterionView"/>.</summary>
public static class AcceptanceCriterionStatuses
{
    public const string Met = "met";
    public const string Unmet = "unmet";
    public const string NotApplicable = "not_applicable";
    public const string Unproven = "unproven";
}

/// <summary>
/// p0344b: one (de)serializer for the run-story JSON persisted on the run row
/// (ProgressLedgerJson / AcceptanceJson) — camelCase, so the stored payload IS
/// the wire payload the dashboard reads. Deserialization is lenient: a corrupt
/// column yields null (served as an honest empty state), never a 500.
/// </summary>
public static class RunStoryJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? TryDeserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
