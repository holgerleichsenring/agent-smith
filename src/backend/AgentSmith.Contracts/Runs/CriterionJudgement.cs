using System.Security.Cryptography;
using System.Text;

namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-08-25-e257: one human judgement of one criterion's disposition, as it is served and
/// as it is recorded.
/// <para>
/// It is a LABEL, not a control. It does not re-open the gate, re-run the phase or move the
/// run's state — a control that changed the outcome would be pressed to ship things, and the
/// label would then measure impatience rather than correctness.
/// </para>
/// </summary>
/// <param name="MachineStatus">What the account said, in the
/// <see cref="AcceptanceCriterionStatuses"/> vocabulary.</param>
/// <param name="HumanStatus">What was actually true, in the same vocabulary.</param>
public sealed record CriterionJudgement(
    string Criterion,
    string MachineStatus,
    string HumanStatus,
    string Reason,
    string Author,
    DateTimeOffset RecordedAt);

/// <summary>What the operator sends to record or withdraw one.</summary>
public sealed record CriterionJudgementRequest(
    string Criterion,
    string MachineStatus,
    string HumanStatus,
    string Reason);

/// <summary>
/// 2026-08-25-e257: how a judgement finds its criterion again.
/// <para>
/// A digest of the normalised text, never the position: the criteria of a re-derived phase
/// can reorder, and a label that silently moved to a different criterion is worse than no
/// label at all. Normalisation is whitespace only — two criteria differing in wording are
/// two criteria, and deciding otherwise is a judgement no index should make.
/// </para>
/// </summary>
public static class CriterionKey
{
    public static string Of(string criterion)
    {
        ArgumentNullException.ThrowIfNull(criterion);
        var normalised = string.Join(' ',
            criterion.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(normalised)))[..32].ToLowerInvariant();
    }
}
