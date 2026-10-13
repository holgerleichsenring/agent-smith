using AgentSmith.Contracts.Runs;
using AgentSmith.Server.Services.Events;
using RunPullRequestView = AgentSmith.Contracts.Runs.RunPullRequestView;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59a: one attempt at a ticket, numbered from 1 by run id. The number is computed
/// per request, so deleting a run renumbers the rest.
/// </summary>
/// <param name="PullRequests">As the run recorded them, one per repository.</param>
/// <param name="ExecutedSpecs">The specs the run executed, each with its verdict.</param>
/// <param name="PendingQuestion">Set while the attempt is parked on a question.</param>
/// <param name="Cause">2026-10-13-9a59d: why the attempt began; null before it fetched its ticket.</param>
/// <param name="Revision">2026-10-13-9a59d: the series revision and approval it worked from; null when
/// it published none.</param>
public sealed record TicketAttemptView(
    int Number,
    string RunId,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    decimal Cost,
    bool EndedOnInfrastructure,
    bool HandedBack,
    IReadOnlyList<RunPullRequestView> PullRequests,
    IReadOnlyList<FiledWorkSpecView> ExecutedSpecs,
    PendingQuestionInfo? PendingQuestion,
    AttemptCause? Cause = null,
    TicketAttemptRevisionView? Revision = null);
