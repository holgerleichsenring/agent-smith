using System.Collections.Concurrent;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Decides per Jira project whether to drive the lifecycle via native status transitions
/// (Pending/Enqueued/InProgress/Done/Failed must exist as real workflow statuses) or via
/// labels. For p95b the probe is a stub that selects Label-mode unconditionally; p95c
/// enables native probing once the full lifecycle config lands. Failure to probe always
/// falls back to Label-mode with a warning.
/// </summary>
public enum JiraLifecycleMode { Native, Label }

public sealed class JiraWorkflowCatalog(ILogger<JiraWorkflowCatalog> logger)
{
    private readonly ConcurrentDictionary<string, JiraLifecycleMode> _modes = new();

    public JiraLifecycleMode GetModeForProject(string projectKey)
        => _modes.GetOrAdd(projectKey, key =>
        {
            logger.LogInformation(
                "Jira project '{Key}' using Label-mode lifecycle (native probing lands in p95c)", key);
            return JiraLifecycleMode.Label;
        });

    internal void SetModeForTest(string projectKey, JiraLifecycleMode mode)
        => _modes[projectKey] = mode;
}
