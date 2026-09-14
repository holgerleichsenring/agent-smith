using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0315b: lazy READ-ONLY sandbox over one repo of a spec-dialog scope. Nothing spawns
/// until the first content read; ReadFile / ListFiles / Grep / DirectoryTree are served and
/// Run / WriteFile come back as failed step results. Owner disposes per turn; the agent's
/// idle self-exit and the orphan reaper are the backstops.
/// <para>2026-09-13-9802: a scope may name a revision. The git ladder that lands on it, and
/// the five refusals it tells apart, live in <see cref="SourceScopeMaterialiser"/> — which
/// also issues the clone, before this guard applies. Here: the guard and the lifetime.</para>
/// </summary>
public sealed class SourceScopeSandbox(
    ResolvedProject project,
    RepoConnection repo,
    string? revision,
    SourceScopeMaterialiser materialiser,
    ISandboxFactory sandboxFactory,
    SandboxSpecBuilder specBuilder,
    IRunContextAccessor runContext,
    ILogger logger) : ISourceScopeSandbox
{
    private readonly SemaphoreSlim _materializeGate = new(1, 1);
    private ISandbox? _inner;

    public string RepoName => repo.Name;
    public bool IsMaterialized => _inner is not null;
    public string? ResolvedSha { get; private set; }
    public string JobId => _inner?.JobId ?? $"source-scope-{repo.Name}";

    public async Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        if (!IsReadKind(step.Kind))
            return Refuse(step,
                $"Step kind '{step.Kind}' is not available on the read-only source sandbox "
                + "for spec-dialog grounding — only file reads (read_file, grep, "
                + "list_directory, directory_tree) are served.");

        if (string.IsNullOrEmpty(repo.Url)) return Refuse(step, NoCloneUrl().Message);

        try
        {
            var inner = await EnsureMaterializedAsync(cancellationToken);
            return await inner.RunStepAsync(step, progress, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Source scope '{Repo}' failed to materialise", repo.Name);
            return Refuse(step, ex.Message);
        }
    }

    /// <summary>2026-09-13-9802: the preparation a read triggers, made explicit so a caller
    /// that must REFUSE learns the KIND before it spends anything — the typed failure escapes
    /// here instead of becoming a step-refusal sentence.</summary>
    public async Task<string> MaterializeAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(repo.Url)) throw NoCloneUrl();
        await EnsureMaterializedAsync(cancellationToken);
        return ResolvedSha ?? string.Empty;
    }

    private SourceScopeUnavailableException NoCloneUrl() => new(
        SourceScopeFailureKind.NoCloneUrl, repo.Name, revision,
        $"Repo '{repo.Name}' has no clone URL configured — source grounding is "
        + "unavailable for it; answer from the code map or say what is missing.");

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
                "Materialising source scope '{Repo}' at '{Revision}'",
                repo.Name, revision ?? "the clone's own default");
            // language/pipelineName null → generic git-bearing image, p0320a light profile.
            var spec = specBuilder.Build(project, language: null, pipelineName: null)
                with { RunId = runContext.CurrentRunId };
            var created = await sandboxFactory.CreateAsync(spec, ct);
            try
            {
                ResolvedSha = await materialiser.PrepareAsync(created, repo, revision, ct);
            }
            catch
            {
                await created.DisposeAsync();
                throw;
            }
            return _inner = created;
        }
        finally
        {
            _materializeGate.Release();
        }
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
