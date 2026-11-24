using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Pocs.Pipeline;

/// <summary>
/// 2026-11-23-933dd: puts a rendered build pipeline on the source repo's default branch in one checkout-free write.
/// Idempotent: a file equal to what the branch holds is not written, and that is said. A file at that path agent smith
/// did not write (no marker) is never overwritten — the refusal names it. On Azure DevOps the outcome names the YAML, the
/// pipeline build.pipeline the operator creates from it, and the one-time permit of the service connection.
/// </summary>
public sealed class PocPipelineWrite(ISourceProviderFactory sources)
{
    public async Task<CommandResult> WriteAsync(RepoConnection repo, PocTargetConfig target, PocPipelineFile file, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(file);
        var provider = sources.Create(repo);
        var branch = await provider.GetDefaultBranchAsync(ct);
        var held = await provider.TryReadFileAsync(file.Path, branch, ct);
        if (held is not null && !PocPipelineTemplate.IsOurs(held))
            return CommandResult.Fail($"{repo.Name} already holds {file.Path}, which agent smith did not write; move it away or merge "
                + "it by hand, then install again.");
        if (held == file.Content) return CommandResult.Ok($"{repo.Name}:{file.Path} already holds this build pipeline; nothing was written.{After(repo, target, file)}");
        var written = await provider.WriteFilesToBranchAsync(branch, [new RepoFile(file.Path, file.Content)],
            $"poc build pipeline for target {target.Name}", ct);
        return written.Written
            ? CommandResult.Ok($"The build pipeline of target '{target.Name}' is commit {written.CommitSha} in {repo.Name}:{file.Path}.{After(repo, target, file)}")
            : CommandResult.Fail($"The build pipeline of target '{target.Name}' could not be written to {repo.Name}: {written.Error}");
    }

    private static string After(RepoConnection repo, PocTargetConfig target, PocPipelineFile file) => repo.Type != RepoType.AzureDevOps ? string.Empty
        : $" Create the pipeline '{target.Build.Pipeline}' from {file.Path} once, and permit it to use the service connection "
          + $"'{target.Build.AzureServiceConnection}' once, or the first build waits for that approval.";
}
