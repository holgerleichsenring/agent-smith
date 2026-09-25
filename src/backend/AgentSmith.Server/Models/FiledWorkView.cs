using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Events;
using AgentSmith.Server.Services.SpecDialog;

// The persisted per-repo outcome (Runs.PullRequestsJson), not the run-detail snapshot's.
using RunPullRequestView = AgentSmith.Contracts.Runs.RunPullRequestView;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-17-042ej: what the conversation that filed work is now following — its latest
/// filing, resolved to the runs working it. The rows are ticket, then run, then PHASE,
/// because a phase is the unit an operator reasons in.
/// </summary>
/// <param name="Tickets">In filing order. Empty for a dialog id with no open session.</param>
/// <param name="Approved">2026-09-25-8e51d: the specification a person approved for the ticket
/// this conversation BELONGS to. It rides this read rather than the dialog view, which the page
/// refetches after every hub message; a record changes only on an approval.</param>
public sealed record FiledWorkView(
    string DialogId,
    IReadOnlyList<FiledWorkTicketView> Tickets,
    ApprovedSetView? Approved = null)
{
    /// <summary>A dialog id nobody has a conversation open on follows nothing.</summary>
    public static FiledWorkView Empty(string dialogId) => new(dialogId, []);
}

/// <summary>
/// One ticket with the runs that took it up: one the filing created, or (2026-09-25-c4a6) the one
/// this conversation is BOUND to, whose Reference is the tracker's own id because a reference is a
/// created ticket's url. A slice record (2026-09-17-0e79d) carries no run: nothing routes it.
/// </summary>
/// <param name="Start">What the ticket became at filing (2026-09-17-042eg), or NotFiled for a bound
/// ticket. Null on a filing written before that phase, which reads as unknown.</param>
/// <param name="Handback">The hand-back case the spec set last recorded, or null.</param>
public sealed record FiledWorkTicketView(
    string Reference, string? Key, string Title, string? TicketId, string? Project,
    FiledWorkStart? Start, IReadOnlyList<FiledWorkRunView> Runs, FiledWorkHandbackView? Handback);

/// <summary>
/// One run of the work ticket, in one of the projects sharing the filing project's tracker.
/// </summary>
/// <param name="PullRequests">As the run recorded them, one per repository. Whether a person
/// merged one is read on the tracker, not here.</param>
/// <param name="PendingQuestion">Set while the run is parked on a dialog question.</param>
public sealed record FiledWorkRunView(
    string RunId,
    string Project,
    string Pipeline,
    string Status,
    decimal CostUsd,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<RunPullRequestView> PullRequests,
    IReadOnlyList<FiledWorkPhaseView> Phases,
    PendingQuestionInfo? PendingQuestion);

/// <summary>
/// One phase of that run, as its own row says it stands.
/// <para>
/// There is deliberately NO "awaiting an amendment" flag here. A hand-back has a status of its
/// own — <c>handed_back</c>, written by RunPhaseProjection.StatusOf for
/// PhaseRunState.HandedBack (2026-09-17-0e79c) — so <paramref name="Status"/> IS the
/// discriminator and the page maps it, once, where the affordance lives. Inferring it from
/// "failed with a verdict" would tell an operator whose build went red to approve the set
/// again, which changes nothing: the only producer of a <c>failed</c> row is the phase's own
/// verification, whose verdict is the failing command.
/// </para>
/// </summary>
/// <param name="Status">Verbatim from the row, so the page can tell a red build from a false
/// premise without parsing the verdict.</param>
/// <param name="Verdict">Rendered only where <paramref name="Status"/> is terminal: the
/// projection KEEPS the last verdict it saw, so a phase running again after a repair would
/// otherwise still show what stopped the previous attempt.</param>
/// <param name="Review">Null when the phase never reached its review step.</param>
public sealed record FiledWorkPhaseView(
    string PhaseId,
    int Ordinal,
    string Title,
    string Status,
    string? Verdict,
    FiledWorkReviewView? Review);

/// <summary>
/// 2026-09-17-042eh's report for one phase, as the page has to tell its FOUR states apart:
/// reviewed with an empty list is a fresh instance that read the whole diff and objected to
/// nothing; NOT reviewed names which of the four reasons it was; a row that cannot be read
/// says so; and no row at all — a null review — is a phase that never reached its review.
/// Rendering any of the last three as the first is the defect 042eh's report exists to stop.
/// </summary>
/// <param name="Why">Why the review was not taken, or why the row could not be read. The
/// producer's contract is "non-null exactly when not reviewed", and
/// <see cref="PhaseReviewReport.None"/> is publishable, so the reader supplies
/// <see cref="NoReasonRecorded"/> rather than rendering an empty sentence.</param>
/// <param name="Findings">What the review kept — shown whether or not the review was taken,
/// because a report can carry both. A finding carrying <c>Reverted</c> means the branch
/// still holds the code the finding is about.</param>
/// <param name="Unreadable">The artifact exists and could not be deserialized. A state of
/// its own: read as "never reviewed" it would turn a phase whose review kept three findings
/// into a phase nobody read.</param>
public sealed record FiledWorkReviewView(
    bool Reviewed, string? Why, IReadOnlyList<PhaseFinding> Findings, bool Unreadable = false)
{
    /// <summary>What a review that was not taken says when the producer recorded no reason.</summary>
    public const string NoReasonRecorded = "no reason was recorded";

    /// <summary>What a row nobody can deserialize says.</summary>
    public const string CouldNotBeRead = "the review recorded for this phase could not be read";
}

/// <summary>
/// The last hand-back the ticket's spec set recorded (TicketSpecSet). The QUESTION itself is
/// a ticket comment and lives on the ticket; this names the case and how often it repeated.
/// </summary>
public sealed record FiledWorkHandbackView(string Case, int Repeated);
