using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// p0380: writes decisions as per-phase / per-run YAML files under
/// <c>.agentsmith/decisions/</c> — the SAME format + location the IDE-side
/// plugin writes (decision.schema.json), retiring the legacy drifted
/// <c>.agentsmith/decisions.md</c> append (p0100 format). Run decisions land
/// in <c>decisions/&lt;runId&gt;.yaml</c> (the revived `run:` slot), parallel
/// to phase decisions. Thread-safe via SemaphoreSlim for concurrent pipeline
/// runs; mirrors each entry into the per-run event stream (p0169e, unchanged).
/// </summary>
public sealed class FileDecisionLogger(
    IEventPublisher eventPublisher,
    IRunContextAccessor runContext,
    ILogger<FileDecisionLogger> logger) : IDecisionLogger
{
    private const string DecisionsDir = ".agentsmith/decisions";
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task LogAsync(string? repoPath, DecisionCategory category,
                               string decision, CancellationToken cancellationToken = default,
                               string? sourceLabel = null)
    {
        await DecisionEventMirror.PublishAsync(
            eventPublisher, runContext, category, decision, sourceLabel, cancellationToken);

        if (string.IsNullOrEmpty(repoPath))
        {
            logger.LogDebug("No repo path provided, skipping file write for [{Category}]: {Decision}",
                category, decision);
            return;
        }
        var label = DecisionFileLabel.Resolve(sourceLabel, runContext.CurrentRunId);
        if (label is null)
        {
            logger.LogDebug(
                "No phase label and no run scope — decision mirrored to the event stream only: [{Category}] {Decision}",
                category, decision);
            return;
        }
        await AppendAsync(repoPath, label, category, decision, cancellationToken);
    }

    private async Task AppendAsync(string repoPath, DecisionFileLabel label,
                                   DecisionCategory category, string decision, CancellationToken ct)
    {
        var path = Path.Combine(repoPath, DecisionsDir, label.FileName);
        await _lock.WaitAsync(ct);
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (directory is not null && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var content = File.Exists(path) ? await File.ReadAllTextAsync(path, ct) : label.Header;
            content += DecisionYamlFormatter.FormatItem(category, decision);
            await File.WriteAllTextAsync(path, content, ct);
            logger.LogDebug("Logged decision [{File}/{Category}]: {Decision}",
                label.FileName, category, decision);
        }
        finally
        {
            _lock.Release();
        }
    }
}
