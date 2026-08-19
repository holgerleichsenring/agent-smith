using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>
/// p0457: reads an operator's answer off the parked run's ticket, and tells the ticket when
/// the run picks up again.
/// <para>
/// The read is a POLL because Azure DevOps has no work-item comment webhook to rely on here.
/// It is not a second resume path: it writes the SAME first-answer-wins inbox the dashboard
/// writes, and <see cref="DialogueResumeSweeper"/> resumes exactly as it always did — which
/// is also where the idempotence comes from, since a comment re-read on the next scan is
/// dropped by the inbox's unique index rather than by a cursor that could disagree with it.
/// </para>
/// </summary>
public sealed class ParkedTicketDialogue(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    ITicketProviderFactory ticketFactory,
    IDialogueAnswerInbox inbox,
    ILogger<ParkedTicketDialogue> logger) : IParkedTicketDialogue
{
    public async Task<bool> TryCollectAnswerAsync(RunCheckpointRecord checkpoint, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        var project = Project(checkpoint);
        if (project is null || !IsPollable(project.Tracker.Type)) return false;

        var reply = await FirstOperatorReplyAsync(project.Tracker, checkpoint, ct);
        if (reply is null) return false;

        var answer = new DialogAnswer(
            checkpoint.QuestionId, reply.Body.Trim(), "ticket-comment", reply.CreatedAt, reply.Author);
        var delivered = await inbox.TryDeliverAsync(checkpoint.DialogueJobId, answer, ct);
        if (delivered)
            logger.LogInformation(
                "Run {RunId}: answer read from ticket {Ticket} (comment by {Author} at {At})",
                checkpoint.RunId, checkpoint.TicketId, reply.Author, reply.CreatedAt);
        return delivered;
    }

    public async Task MoveToInProgressAsync(RunCheckpointRecord checkpoint, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        var project = Project(checkpoint);
        if (project is null) return;

        var target = TriggerSelectionHelper.ByTrackerType(project, project.Tracker.Type)?.InProgressStatus;
        if (string.IsNullOrWhiteSpace(target))
        {
            // Nothing is invented here — but the operator hears about it, because the board
            // now says "waiting for you" over a run that is working again.
            logger.LogInformation(
                "Run {RunId} resumed but ticket {Ticket} stays in its clarification status — "
                + "project '{Project}' declares no in_progress_status",
                checkpoint.RunId, checkpoint.TicketId, project.Name);
            return;
        }

        await ticketFactory.Create(project.Tracker)
            .TransitionToAsync(new TicketId(checkpoint.TicketId), target!, ct);
        logger.LogInformation(
            "Run {RunId} resumed — ticket {Ticket} moved to '{Status}'",
            checkpoint.RunId, checkpoint.TicketId, target);
    }

    /// <summary>
    /// The OLDEST comment that is neither ours nor older than the question. Older is not an
    /// answer to something that did not exist yet, and our own open-questions comment is the
    /// newest thing on the ticket the moment we start looking — reading it back would answer
    /// the question with itself.
    /// </summary>
    private async Task<TicketComment?> FirstOperatorReplyAsync(
        TrackerConnection tracker, RunCheckpointRecord checkpoint, CancellationToken ct)
    {
        try
        {
            var comments = await ticketFactory.Create(tracker)
                .GetCommentsAsync(new TicketId(checkpoint.TicketId), ct);
            return comments
                .Where(c => c.CreatedAt > checkpoint.AskedAt && !OwnTicketComment.IsOurs(c))
                .Where(c => !string.IsNullOrWhiteSpace(c.Body))
                .OrderBy(c => c.CreatedAt)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            // A tracker that is briefly unreachable must not stop the sweep for every other
            // parked run — the next scan asks again.
            logger.LogWarning(ex,
                "Could not read ticket {Ticket} for run {RunId} while looking for an answer",
                checkpoint.TicketId, checkpoint.RunId);
            return null;
        }
    }

    /// <summary>
    /// Only Azure DevOps is polled this phase — that is where it was paid for. Another
    /// tracker joins by naming itself here, once its ticket comments are known to reach a
    /// checkpoint rather than merely spawn a fresh run.
    /// </summary>
    private static bool IsPollable(TrackerType type) => type == TrackerType.AzureDevOps;

    private ResolvedProject? Project(RunCheckpointRecord checkpoint)
    {
        if (string.IsNullOrWhiteSpace(checkpoint.Project) || string.IsNullOrWhiteSpace(checkpoint.TicketId))
            return null;
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        return config.Projects.TryGetValue(checkpoint.Project, out var project) ? project : null;
    }
}
