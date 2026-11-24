using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Pocs.Commit;

/// <summary>
/// 2026-11-23-933dc: writes ONE folder of a shared repository as one commit on its default branch — a fresh sandbox of
/// the generic git image (no project secrets), a sparse clone of that folder only, the folder replaced by the given
/// files, and a plain push with one rebase retry. When the folder already equals the files nothing is committed and
/// the last commit touching the folder is the answer. The c81e series writes &lt;deploy_root&gt;/&lt;poc&gt;/ the same way.
/// </summary>
public sealed class PocFolderWrite(
    ISandboxFactory factory, SandboxSpecBuilder specs, IRunContextAccessor runContext, PocSparseCheckout checkout,
    PocFolderReplace replace, PocFolderPush push, SandboxGitOperations operations)
{
    public async Task<string> WriteAsync(
        ResolvedProject project, RepoConnection repo, string folder, IReadOnlyList<ReferenceSetFile> files, string message,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        var spec = specs.Build(project, language: null, pipelineName: null) with
        {
            Secrets = ResolvedSandboxSecrets.Empty, NoPackageCaches = true, RunId = runContext.CurrentRunId,
        };
        await using var sandbox = await factory.CreateAsync(spec, ct);
        var branch = await checkout.OpenAsync(sandbox, repo, folder, ct);
        await replace.ReplaceAsync(sandbox, folder, files, ct);
        if (await operations.HasStagedChangesAsync(sandbox, ct))
            return await push.CommitAndPushAsync(sandbox, repo, branch, message, ct);
        var last = await operations.GetLastCommitForPathAsync(sandbox, folder, ct);
        return last.Length > 0 ? last : throw new InvalidOperationException($"'{folder}' holds nothing to commit and has no commit yet");
    }
}
