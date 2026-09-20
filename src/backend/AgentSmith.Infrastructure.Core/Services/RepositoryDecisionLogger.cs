using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// p0380: writes decisions as per-phase / per-run YAML files under
/// <c>.agentsmith/decisions/</c> — the SAME format + location the IDE-side
/// plugin writes (decision.schema.json), retiring the legacy drifted
/// <c>.agentsmith/decisions.md</c> append (p0100 format). Run decisions land
/// in <c>decisions/&lt;runId&gt;.yaml</c> (the revived `run:` slot), parallel
/// to phase decisions; each entry is mirrored into the per-run event stream (p0169e).
/// <para>
/// 2026-09-19-c511a: the file goes through the REPOSITORY's surface, not the host's. The path this
/// was given is the sandbox mount /work, so in a container every write went to the server process's
/// own root, where that directory does not exist and nothing may be created — one log_decision
/// ended a coding run whose work was already done. See <see cref="RepositoryDecisionFile"/>.
/// </para>
/// <para>
/// 2026-09-19-c511b: and it never throws. An audit sink that can end a run is a work step by
/// accident. The two records fail separately, so the outcome names which of them took the decision:
/// the run's event stream is the record, the repository file a copy that travels with the code.
/// </para>
/// </summary>
public sealed class RepositoryDecisionLogger : IDecisionLogger
{
    private readonly IRunContextAccessor _runContext;
    private readonly DecisionEventMirror _eventMirror;
    private readonly ILogger<RepositoryDecisionLogger> _logger;
    private readonly RepositoryDecisionFile _file;

    public RepositoryDecisionLogger(
        IRunContextAccessor runContext,
        DecisionEventMirror eventMirror,
        ILogger<RepositoryDecisionLogger> logger)
    {
        _runContext = runContext;
        _eventMirror = eventMirror;
        _logger = logger;
        _file = new RepositoryDecisionFile(logger);
    }

    public async Task<DecisionLogOutcome> LogAsync(
        ISandboxFileReader? repositoryFiles, DecisionCategory category,
        string decision, CancellationToken cancellationToken = default,
        string? sourceLabel = null)
    {
        if (!await MirrorAsync(category, decision, sourceLabel, cancellationToken))
            return DecisionLogOutcome.NotRecorded;

        if (repositoryFiles is null)
        {
            _logger.LogDebug("No repository surface, skipping file write for [{Category}]: {Decision}",
                category, decision);
            return DecisionLogOutcome.Recorded;
        }
        var label = DecisionFileLabel.Resolve(sourceLabel, _runContext.CurrentRunId);
        if (label is null)
        {
            _logger.LogDebug(
                "No phase label and no run scope — decision mirrored to the event stream only: "
                + "[{Category}] {Decision}", category, decision);
            return DecisionLogOutcome.Recorded;
        }
        return await _file.AppendAsync(repositoryFiles, label, category, decision, cancellationToken)
            ? DecisionLogOutcome.Recorded
            : DecisionLogOutcome.RecordedWithoutRepositoryCopy;
    }

    private async Task<bool> MirrorAsync(
        DecisionCategory category, string decision, string? sourceLabel, CancellationToken ct)
    {
        try
        {
            await _eventMirror.PublishAsync(category, decision, sourceLabel, ct);
            return true;
        }
        catch (Exception ex) when (!RepositoryDecisionFile.Cancelled(ex, ct))
        {
            _logger.LogWarning(ex,
                "Decision NOT recorded — the run's event stream refused it ({Reason}): "
                + "[{Category}] {Decision}", ex.Message, category, decision);
            return false;
        }
    }
}
