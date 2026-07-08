using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0315b: lazy READ-ONLY sandbox over one repo of a spec-dialog scope.
/// Creation is free — the underlying sandbox (generic git-bearing image, no
/// toolchain resolution, no build) is spawned and the repo cloned only when
/// the first content-read step arrives, so a design turn that never needs
/// file content never spawns anything. The read-only contract is expressed
/// in the existing step vocabulary: ReadFile / ListFiles / Grep /
/// DirectoryTree are served, Run / WriteFile come back as failed step
/// results (the clone itself is issued by THIS class against the inner
/// sandbox, before the guard applies). Owner disposes per turn; the sandbox
/// agent's idle self-exit and the orphan reaper are the backstops.
/// </summary>
public sealed class SourceScopeSandbox(
    ResolvedProject project,
    RepoConnection repo,
    ISandboxFactory sandboxFactory,
    SandboxSpecBuilder specBuilder,
    IRunContextAccessor runContext,
    ILogger logger) : ISourceScopeSandbox
{
    private readonly SemaphoreSlim _materializeGate = new(1, 1);
    private ISandbox? _inner;

    public string RepoName => repo.Name;
    public bool IsMaterialized => _inner is not null;
    public string JobId => _inner?.JobId ?? $"source-scope-{repo.Name}";

    public async Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        if (!IsReadKind(step.Kind))
            return Refuse(step,
                $"Step kind '{step.Kind}' is not available on the read-only source sandbox "
                + "for spec-dialog grounding — only file reads (read_file, grep, "
                + "list_directory, directory_tree) are served.");

        if (string.IsNullOrEmpty(repo.Url))
            return Refuse(step,
                $"Repo '{repo.Name}' has no clone URL configured — source grounding is "
                + "unavailable for it; answer from the code map or say what is missing.");

        ISandbox inner;
        try
        {
            inner = await EnsureMaterializedAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Read-only source sandbox for '{Repo}' failed to materialise", repo.Name);
            return Refuse(step,
                $"The read-only source sandbox for '{repo.Name}' could not be prepared: "
                + $"{ex.Message}");
        }
        return await inner.RunStepAsync(step, progress, cancellationToken);
    }

    private static bool IsReadKind(StepKind kind) => kind is
        StepKind.ReadFile or StepKind.ListFiles or StepKind.Grep or StepKind.DirectoryTree;

    private async Task<ISandbox> EnsureMaterializedAsync(CancellationToken ct)
    {
        if (_inner is not null) return _inner;
        await _materializeGate.WaitAsync(ct);
        try
        {
            if (_inner is not null) return _inner;
            logger.LogInformation(
                "Materialising read-only source sandbox for '{Repo}' (first content read)", repo.Name);
            // language: null → the builder's generic git-bearing fallback image;
            // no toolchain, no build — clone + read is the entire contract.
            var spec = specBuilder.Build(project, language: null)
                with { RunId = runContext.CurrentRunId };
            var created = await sandboxFactory.CreateAsync(spec, ct);
            await CloneOrThrowAsync(created, ct);
            _inner = created;
            return _inner;
        }
        finally
        {
            _materializeGate.Release();
        }
    }

    private async Task CloneOrThrowAsync(ISandbox created, CancellationToken ct)
    {
        var clone = await created.RunStepAsync(CheckoutStepFactory.BuildCloneStep(repo), null, ct);
        if (clone.ExitCode == 0) return;
        await created.DisposeAsync();
        throw new InvalidOperationException(
            $"git clone of '{repo.Name}' failed (exit={clone.ExitCode}): {clone.ErrorMessage}");
    }

    private static StepResult Refuse(Step step, string reason) => new(
        StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 1,
        TimedOut: false, DurationSeconds: 0, ErrorMessage: reason, OutputContent: null);

    public async ValueTask DisposeAsync()
    {
        var inner = _inner;
        _inner = null;
        if (inner is not null) await inner.DisposeAsync();
        _materializeGate.Dispose();
    }
}
