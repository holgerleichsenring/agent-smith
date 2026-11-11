namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-11-10-d854c: the proposal one design turn made, keyed by the turn's RUN id — beside the latest proposal on the
/// session row, which a decision acts on and a later turn replaces. A draft under review is written here as it is
/// pushed and replaced by the turn's final proposal, so a turn that dies mid-review still leaves its last draft.
/// </summary>
public sealed class SpecDialogTurnProposal : EntityBase
{
    public long Id { get; set; }

    /// <summary>The conversation's session id.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>The turn's run id; one row per run.</summary>
    public string RunId { get; set; } = string.Empty;

    /// <summary>The proposal, as the session row's proposal columns store one.</summary>
    public string ProposalJson { get; set; } = string.Empty;
}
