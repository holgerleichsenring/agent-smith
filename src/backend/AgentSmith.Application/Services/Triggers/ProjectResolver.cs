using AgentSmith.Application.Services.Metrics;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Triggers;

/// <summary>
/// p0140a: turns an incoming ticket envelope (labels + area-path + source-repo-url +
/// to-address) into the list of (project, pipeline) tuples that match it.
/// Stateless; pure function over config + envelope.
///
/// Ambiguous resolution is intentional: a ticket that matches more than one project is
/// returned with all matches. p0140b's webhook handlers will spawn a pipeline run for
/// each. Zero matches return an empty list — the handler decides whether to log,
/// comment, or just drop.
///
/// Within a matched project, pipeline selection delegates to <see cref="PipelineResolver"/>
/// so the existing pipeline-from-label / default-pipeline / global-fallback chain stays
/// authoritative. The resolver itself is project-only.
/// </summary>
public sealed class ProjectResolver(
    AgentSmithMetrics metrics,
    PipelineResolver pipelineResolver,
    ILogger<ProjectResolver>? logger = null, IStartupFindings? findings = null)
    : IEnvelopeProjectResolver
{
    public IReadOnlyList<ProjectMatch> Resolve(AgentSmithConfig config, IncomingTicketEnvelope envelope)
    {
        // 2026-09-13-a3f1: refused before every other rule — each of those ends in something,
        // and on a project with no pipeline_from_label that something is DefaultPipeline.
        // 2026-09-22-b3d7: nothing files this label any more; the refusal stays, permanently, for
        // the two generations that carry it and are still on a board (see EpicLabel).
        if (FiledTicketLabels.IsEpicRecord(envelope))
        {
            logger?.LogInformation("ProjectResolver: '{Label}' marks a record, not work",
                PhaseTicketRenderer.EpicLabel);
            return [];
        }

        var matches = new List<ProjectMatch>();
        foreach (var (projectName, project) in config.Projects)
        {
            foreach (var (kind, trigger) in EnumerateTriggers(project))
            {
                if (IsBlocked(projectName, kind)) continue;
                if (!TriggerEnvelopeMatch.Matches(trigger, project, envelope))
                {
                    // Per-project drop reason — so an empty match set is explained ticket-by-ticket.
                    logger?.LogDebug(
                        "ProjectResolver: project '{Project}' did NOT match on {Kind} — resolution "
                        + "{Strategy}='{Value}' vs labels=[{Labels}] areaPath='{Area}' repo='{Repo}'",
                        projectName, kind, trigger.ProjectResolution?.Strategy,
                        trigger.ProjectResolution?.Value, string.Join(",", envelope.Labels),
                        envelope.AreaPath ?? "", envelope.SourceRepoUrl ?? "");
                    continue;
                }

                // p0315d: a ticket that BINDS routes hard-bound to phase execution on every
                // project it matches — BEFORE pipeline_from_label, which would otherwise drop it:
                // no operator's label map holds the framework's own stamp.
                // 2026-09-22-766b: a FILING binds on the APPROVAL rather than on a word it wrote
                // for itself; a PERSON still binds by typing the phase word. Load-bearing on the
                // WEBHOOK, which resolves a repository and an area path a poll cannot.
                var pipeline = FiledTicketLabels.BindsPhaseExecution(envelope)
                    ? PipelinePresets.PhaseExecutionName
                    : pipelineResolver.Resolve(
                        trigger, envelope.Labels, config.PipelineTriggers, logger as ILogger);

                if (string.IsNullOrEmpty(pipeline))
                {
                    // No DefaultPipeline fallback: when pipeline_from_label is set it acts as a strict filter; an unmatched ticket must be dropped.
                    logger?.LogInformation(
                        "ProjectResolver: project '{Project}' matched envelope on {Kind} but no "
                        + "pipeline_from_label entry matched labels=[{Labels}]; dropping.",
                        projectName, kind, string.Join(",", envelope.Labels));
                    continue;
                }

                logger?.LogDebug(
                    "ProjectResolver: project '{Project}' matched on {Kind} → pipeline '{Pipeline}'",
                    projectName, kind, pipeline);
                matches.Add(new ProjectMatch(projectName, pipeline, kind));
            }
        }
        AmbiguousResolutionMetric.EmitIfAmbiguous(metrics, matches);
        return matches;
    }

    // p0391a: a trigger carrying a blocking startup finding is not started — a run it
    // spawned could not complete the thing the finding names (park a question, terminalize
    // a ticket) and would loop. Both the webhook dispatch and the poller's claim path
    // resolve through here, so the trigger is refused, never the process.
    private bool IsBlocked(string projectName, string matchKind)
    {
        if (findings is null) return false;
        var reason = findings.BlockingReason(projectName, TriggerKinds.ForMatchKind(matchKind));
        if (reason is null) return false;
        logger?.LogWarning(
            "ProjectResolver: project '{Project}' {Kind} is disabled by a startup finding — {Reason}",
            projectName, matchKind, reason);
        return true;
    }

    private static IEnumerable<(string Kind, WebhookTriggerConfig Trigger)> EnumerateTriggers(ResolvedProject project)
        => TriggerEnvelopeMatch.Triggers(project);

}
