using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-10-864f: uploaded material as a reviewer reads it — the four reads and nothing else, so a review that
/// checks a spec against an upload can never run a command or write. Two sources stand behind it: a design turn's
/// held upload (read without counting as what the turn opened), and a run's carried copy inside the carrying
/// repository, whose paths are rooted at the copy so <c>reference:&lt;name&gt;/&lt;path&gt;</c> addresses both alike.
/// </summary>
public sealed class ReviewReferenceView : ISourceScopeSandbox
{
    private readonly Func<Step, CancellationToken, Task<StepResult>> _run;
    private readonly Func<bool> _materialized;
    private readonly Func<CancellationToken, Task<string>> _materialize;
    private readonly string _root;

    private ReviewReferenceView(string address, string jobId, string root, Func<Step, CancellationToken, Task<StepResult>> run,
        Func<bool> materialized, Func<CancellationToken, Task<string>> materialize)
    {
        (RepoName, JobId, _root, _run, _materialized, _materialize) = (address, jobId, root, run, materialized, materialize);
    }

    /// <summary>A design turn's held upload, under <paramref name="address"/>; a set's reads are not counted as the turn's.</summary>
    public static ReviewReferenceView Over(string address, ISourceScopeSandbox upload) => new(
        address, upload.JobId, string.Empty,
        upload is ReferenceSetSandbox set ? set.RunUnrecordedAsync : (step, ct) => upload.RunStepAsync(step, null, ct),
        () => upload.IsMaterialized, upload.MaterializeAsync);

    /// <summary>A run's carried copy at <paramref name="root"/> inside <paramref name="repository"/>.</summary>
    public static ReviewReferenceView Carried(string address, ISandbox repository, string root) => new(
        address, repository.JobId, root.Trim('/'), (step, ct) => repository.RunStepAsync(step, null, ct), () => true,
        _ => Task.FromResult(string.Empty));

    public string RepoName { get; }

    public string JobId { get; }

    public bool IsMaterialized => _materialized();

    public string? ResolvedSha => null;

    public Task<string> MaterializeAsync(CancellationToken cancellationToken) => _materialize(cancellationToken);

    public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (step.Kind is not (StepKind.ReadFile or StepKind.ListFiles or StepKind.Grep or StepKind.DirectoryTree))
            return Task.FromResult(SourceScopeRefusal.Because(step,
                $"A review reads uploaded material; step kind '{step.Kind}' is not served on '{RepoName}'."));
        return _run(_root.Length == 0 ? step : step with { Path = Rooted(step.Path) }, cancellationToken);
    }

    // A path arrives relative or under the work root ("/work/x"); either way the copy's root goes in front of it.
    private string Rooted(string? path)
    {
        const string work = Domain.Entities.Repository.SandboxWorkPath;
        var absolute = path is not null && (path == work || path.StartsWith(work + "/", StringComparison.Ordinal));
        var rest = (absolute ? path![work.Length..] : path ?? string.Empty).Trim('/');
        var rooted = rest.Length == 0 || rest == "." ? _root : $"{_root}/{rest}";
        return absolute ? $"{work}/{rooted}" : rooted;
    }

    /// <summary>The view owns nothing: the turn or the run disposes what stands behind it.</summary>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
