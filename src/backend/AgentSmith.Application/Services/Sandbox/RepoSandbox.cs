using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-25-9992c: one repository of a run, as its sandbox. The repository lives at
/// <see cref="Repository.WorkPathFor"/> beside the run's other repositories, so a step that
/// names no working directory runs there and a relative working directory or path resolves
/// under it; an absolute one is the caller's and passes through. This is the whole reason
/// the type exists — every caller that used to assume the repository IS /work asks the
/// sandbox instead, and the backend below sees only absolute paths, whatever it is.
/// </summary>
public sealed class RepoSandbox(ISandbox inner, string repoName)
    : ISandbox, ISandboxWrapper, ISandboxWorkDirectory
{
    public string RepoName => repoName;

    public string WorkDirectory { get; } = Repository.WorkPathFor(repoName);

    public ISandbox Inner => inner;

    public string JobId => inner.JobId;

    public Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken) =>
        inner.RunStepAsync(Rooted(step), progress, cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();

    // A relative path resolves under the step's own working directory, as the agent resolves it.
    // 2026-11-25-9992d: the step is stamped with this repository as its secret scope.
    internal Step Rooted(Step step)
    {
        var directory = Under(WorkDirectory, step.WorkingDirectory) ?? WorkDirectory;
        return step with
        {
            WorkingDirectory = directory,
            Path = Under(directory, step.Path),
            RenameTo = Under(directory, step.RenameTo),
            // 2026-11-25-9992d: a command of this repository receives its scoped secrets and no other repository's.
            SecretScope = repoName,
        };
    }

    private static string? Under(string root, string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (path.StartsWith('/')) return path;
        if (path is "." or "./") return root;
        return $"{root}/{(path.StartsWith("./", StringComparison.Ordinal) ? path[2..] : path)}";
    }
}
