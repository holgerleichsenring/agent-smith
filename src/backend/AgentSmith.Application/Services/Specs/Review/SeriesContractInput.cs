using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762k: everything the one fresh series-contract reviewer is given — the contract and every member.
/// <paramref name="InRun"/> decides what a cut rests on: a read of its own in the run's repositories, or, in a design
/// turn whose drafts exist only in the proposal, the contract entry it breaks, quoted verbatim.
/// </summary>
public sealed record SeriesContractInput(
    SeriesContract Contract, IReadOnlyList<SeriesContractMember> Members, bool InRun, string? QuestionPin = null)
{
    /// <summary>Sandbox key to repository name, as <see cref="SpecAttackInput.RepositoryNames"/>.</summary>
    public IReadOnlyDictionary<string, string> RepositoryNames { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>2026-10-13-8762k: one member as the reviewer sees it; <paramref name="Status"/> is one of the
/// constants in a run and null in a design turn.</summary>
public sealed record SeriesContractMember(SpecDraft Draft, string? Status = null)
{
    public const string Executed = "executed";
    public const string Cleared = "cleared";
    public const string NotCleared = "not cleared";
}
