using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59e: one spec of a published cut — its content from the cut, its standing from the
/// RunSpec rows, and each done criterion with the verdict of the attempt that last ended the spec.
/// </summary>
/// <param name="State">not_started | in_progress | done | failed | handed_back | carried.</param>
/// <param name="ExecutedBy">The attempt that executed the spec; null while nothing has.</param>
/// <param name="Unmeasured">Accounts that could not be taken for the spec, with why.</param>
public sealed record TicketSeriesSpecView(
    string SpecId,
    int Ordinal,
    string FileStem,
    string Goal,
    IReadOnlyList<string> Repos,
    IReadOnlyList<SpecStep> Steps,
    string Yaml,
    string State,
    string? ExecutedBy,
    int? ExecutedByAttempt,
    IReadOnlyList<TicketSeriesCriterionView> Done,
    IReadOnlyList<UnmeasuredAccountView> Unmeasured);
