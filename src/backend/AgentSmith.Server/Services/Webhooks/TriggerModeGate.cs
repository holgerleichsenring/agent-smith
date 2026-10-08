using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-101b: a tracker entry is served by polling or by webhooks, never both. A project whose
/// entry polls gets nothing from a webhook delivery — a ticket delivery drops it from the matches, a
/// pull-request delivery is refused when ANY project declaring the repository polls — and the
/// delivery list says why. Called at each webhook handler's door, before any model or host call; the
/// resolvers the PR sweep shares are not gated.
/// </summary>
public sealed class TriggerModeGate(IConfigurationLoader configLoader, ServerContext serverContext, IConfiguredRepoFinder repoFinder)
{
    public static bool Polls(AgentSmithConfig config, ResolvedProject project) =>
        (config.Trackers.TryGetValue(project.Tracker.Name, out var entry) ? entry : project.Tracker).Polling.Enabled;

    public IReadOnlyList<ProjectMatch> Webhook(AgentSmithConfig config, IReadOnlyList<ProjectMatch> matches) =>
        [.. matches.Where(m => !config.Projects.TryGetValue(m.ProjectName, out var p) || !Polls(config, p))];

    /// <summary>Refused when every match was a polling project's — never the zero-match comment.</summary>
    public WebhookResult? Refusal(AgentSmithConfig config, IReadOnlyList<ProjectMatch> all, IReadOnlyList<ProjectMatch> kept) =>
        all.Count > 0 && kept.Count == 0 ? Refused(config, config.Projects[all[0].ProjectName]) : null;

    public WebhookResult? RepoRefusal(string repoUrl)
    {
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        return repoFinder.FindAll(config, repoUrl).Select(o => o.Project).FirstOrDefault(p => Polls(config, p)) is { } polling
            ? Refused(config, polling) : null;
    }

    private static WebhookResult Refused(AgentSmithConfig config, ResolvedProject project) =>
        WebhookResult.NotHandled($"project {project.Name} polls tracker {project.Tracker.Name}; webhooks start nothing for it");
}
