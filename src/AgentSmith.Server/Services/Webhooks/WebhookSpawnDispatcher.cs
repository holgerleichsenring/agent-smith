using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Shared per-match spawn loop + zero-match handler used by every ticket-event webhook
/// handler. Owns: matching project lookup, per-trigger status-filter check, spawn call,
/// zero-match structured log + opt-in zero-match comment. Keeps handlers thin and the
/// behaviour identical across all 8 platforms.
/// </summary>
public sealed class WebhookSpawnDispatcher(
    ISpawnPipelineRunsUseCase spawnUseCase,
    ITicketProviderFactory providerFactory,
    ILogger<WebhookSpawnDispatcher> logger)
{
    private const string ZeroMatchComment = "No agent-smith project matched this ticket.";

    public async Task DispatchAsync(
        AgentSmithConfig config,
        IReadOnlyList<ProjectMatch> matches,
        IncomingTicketEnvelope envelope,
        string ticketStatus,
        Dictionary<string, string>? planAnswers,
        CancellationToken ct)
    {
        if (matches.Count == 0)
        {
            await HandleZeroMatchAsync(config, envelope, ct);
            return;
        }

        foreach (var match in matches)
            await TrySpawnMatchAsync(config, match, envelope, ticketStatus, planAnswers, ct);
    }

    private async Task TrySpawnMatchAsync(
        AgentSmithConfig config, ProjectMatch match, IncomingTicketEnvelope envelope,
        string ticketStatus, Dictionary<string, string>? planAnswers, CancellationToken ct)
    {
        var project = config.Projects[match.ProjectName];
        var trigger = TriggerSelectionHelper.ByKind(project, match.Kind);
        if (trigger is null) return;

        if (!IsStatusAllowed(trigger, ticketStatus))
        {
            logger.LogInformation(
                "status-filter: ticket {Ticket} status '{Status}' not in trigger_statuses for project '{Project}' — skip",
                envelope.TicketId, ticketStatus, project.Name);
            return;
        }

        await spawnUseCase.ExecuteAsync(
            config, project, match.PipelineName, envelope, trigger, ct, planAnswers);
    }

    private async Task HandleZeroMatchAsync(
        AgentSmithConfig config, IncomingTicketEnvelope envelope, CancellationToken ct)
    {
        logger.LogInformation(
            "zero_match: platform={Platform} ticket_id={Ticket} ticket_url={Url} labels=[{Labels}] area_path={Area}",
            envelope.Platform, envelope.TicketId, envelope.TicketUrl,
            string.Join(",", envelope.Labels), envelope.AreaPath);

        var tracker = FindZeroMatchCommentTracker(config, envelope.Platform);
        if (tracker is null) return;

        var provider = providerFactory.Create(tracker);
        if (!provider.SupportsComments) return;

        try
        {
            await provider.UpdateStatusAsync(new TicketId(envelope.TicketId!), ZeroMatchComment, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Zero-match comment post failed for ticket {Ticket}", envelope.TicketId);
        }
    }

    private static TrackerConnection? FindZeroMatchCommentTracker(AgentSmithConfig config, string? platform)
    {
        if (string.IsNullOrEmpty(platform)) return null;
        var platformType = ParsePlatform(platform);
        if (platformType is null) return null;

        foreach (var tracker in config.Trackers.Values)
            if (tracker.Type == platformType && tracker.ZeroMatchComment) return tracker;
        return null;
    }

    private static TrackerType? ParsePlatform(string platform) => platform.ToLowerInvariant() switch
    {
        "github" => TrackerType.GitHub,
        "gitlab" => TrackerType.GitLab,
        "azuredevops" => TrackerType.AzureDevOps,
        "jira" => TrackerType.Jira,
        _ => null,
    };

    private static bool IsStatusAllowed(WebhookTriggerConfig trigger, string status) =>
        trigger.TriggerStatuses.Count == 0
        || trigger.TriggerStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);
}
