using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Turns;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-02-3f06c: one cited file, read whole out of one of the turn's sandboxes and counted by
/// <see cref="EvidenceLineCount"/>. Only an exit 0 is a file and only the reader's own
/// <see cref="StepErrors.FileNotFoundPrefix"/> is an absence; anything else — a file over the read
/// cap, a refusal a scope wrapped into an exit, a path leaving the root, a throw — is a read that
/// could not tell, and is never reported. A source scope that has not opened is not stepped: the
/// check would clone a repository the turn never read.
/// </summary>
internal sealed class SandboxEvidenceRead(ITurnActivityObserverAccessor activity)
{
    private const int TimeoutSeconds = 90;

    private readonly Dictionary<(string Repository, string Path), EvidenceProbeResult> _seen = [];

    public async Task<EvidenceProbeResult> ReadAsync(
        string repository, ISandbox sandbox, string path, CancellationToken ct)
    {
        if (!ContainedPath.TryRelative(path, out var relative)) return new EvidenceProbeResult.NotChecked();
        if (sandbox is ISourceScopeSandbox { IsMaterialized: false }) return new EvidenceProbeResult.NotChecked();
        if (_seen.TryGetValue((repository, relative), out var known)) return known;

        // Reported as the read tool reports itself, so the turn's owner sees what was opened.
        await activity.ReportAsync(
            new TurnActivity(TurnActivityKind.Tool, RepositoryFileReadTool.Name, $"{repository}/{relative}"), ct);
        var result = await StepAsync(sandbox, relative, ct);
        _seen[(repository, relative)] = result;
        return result;
    }

    private static async Task<EvidenceProbeResult> StepAsync(ISandbox sandbox, string relative, CancellationToken ct)
    {
        var step = new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ReadFile, TimeoutSeconds: TimeoutSeconds,
            Path: ContainedPath.Absolute(relative), WorkingDirectory: Repository.SandboxWorkPath);
        try
        {
            var result = await sandbox.RunStepAsync(step, progress: null, ct);
            if (result.ExitCode == 0 && !result.TimedOut)
                return new EvidenceProbeResult.File(EvidenceLineCount.Of(result.OutputContent ?? string.Empty));
            return StepErrors.IsFileNotFound(result.ErrorMessage)
                ? new EvidenceProbeResult.NotAFile()
                : new EvidenceProbeResult.NotChecked();
        }
        // On the RUN's token: a sandbox that went silent is a read that could not run.
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return new EvidenceProbeResult.NotChecked();
        }
    }
}
