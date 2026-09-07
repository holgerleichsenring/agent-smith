using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: non-progress across runs is MECHANICAL, never a prose comparison — two
/// hand-backs with the same CASE CODE and no source commit on the ticket branch
/// between them end the loop. Comparing LLM-written reasons would never match: the
/// same fact is written differently twice.
/// <para>
/// A REFUSAL is excluded: ending the loop means the run CONTINUES, and continuing past
/// what must not be done is the one wrong answer. Refused again with nothing new said
/// parks again.
/// </para>
/// <para>
/// A QUESTION is excluded too, for the opposite reason: its loop is ended by the
/// CONVERSATION, not by the branch. An unanswered question is pinned into the next
/// derivation as the answer before the model runs, so a second park is the model
/// asking again — and that must park, whatever the shas say.
/// </para>
/// </summary>
public static class SpecHandbackProgress
{
    /// <summary>True when this hand-back repeats the previous one with nothing done between.</summary>
    public static bool RepeatsWithoutProgress(
        SpecSetPointer? pointer, SpecHandbackCase current, string branchHeadSha)
    {
        if (current is SpecHandbackCase.Refused or SpecHandbackCase.Question) return false;
        if (pointer is null || pointer.LastHandbackCase != current) return false;
        if (string.IsNullOrEmpty(pointer.HandbackSourceSha)) return false;
        return string.Equals(pointer.HandbackSourceSha, branchHeadSha, StringComparison.Ordinal);
    }

    /// <summary>The pointer to record after a hand-back was posted.</summary>
    public static SpecSetPointer Record(
        SpecSetPointer pointer, SpecHandbackCase current, string branchHeadSha) =>
        pointer with
        {
            LastHandbackCase = current,
            RepeatedHandbackCount = pointer.LastHandbackCase == current
                ? pointer.RepeatedHandbackCount + 1
                : 1,
            HandbackSourceSha = branchHeadSha,
        };
}
