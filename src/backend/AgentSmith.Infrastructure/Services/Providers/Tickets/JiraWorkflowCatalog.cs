using System.Collections.Concurrent;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Decides per Jira project whether to drive the lifecycle via native status transitions
/// or via labels. Native mode is selected when the tracker configures a
/// <c>lifecycle_status_names</c> map (p0300a) — the operator has named the workflow
/// statuses agent-smith should transition to. Without a map, label mode applies (the
/// default). The mode is decided once per project and cached; the decision is logged
/// once so operators can confirm which mode a project resolved to.
/// </summary>
public enum JiraLifecycleMode { Native, Label }

public sealed class JiraWorkflowCatalog(ILogger<JiraWorkflowCatalog> logger)
{
    private readonly ConcurrentDictionary<string, JiraLifecycleMode> _modes = new();

    public JiraLifecycleMode GetModeForProject(string projectKey, bool nativeConfigured = false)
        => _modes.GetOrAdd(projectKey, key =>
        {
            var mode = nativeConfigured ? JiraLifecycleMode.Native : JiraLifecycleMode.Label;
            logger.LogInformation("Jira project '{Key}' using {Mode}-mode lifecycle", key, mode);
            return mode;
        });

    internal void SetModeForTest(string projectKey, JiraLifecycleMode mode)
        => _modes[projectKey] = mode;
}
