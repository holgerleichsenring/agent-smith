using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>
/// 2026-11-23-c81eb: puts the default app template into &lt;template_path&gt;/app/ of the target's source repo in one
/// checkout-free write — only the files the branch does not hold, so an operator's edit is never overwritten; once
/// written they are the operator's. Nothing absent → nothing written, and that is said.
/// </summary>
public sealed class PocAppTemplateWrite(ISourceProviderFactory sources, PocAppTemplateDefaults defaults)
{
    public async Task<(bool Written, string Sentence)> WriteAsync(RepoConnection repo, PocTargetConfig target, int maxFiles, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(target);
        var provider = sources.Create(repo);
        var branch = await provider.GetDefaultBranchAsync(ct);
        var folder = PocTemplateRead.Folder(target);
        var held = (await provider.ListFilesUnderAsync(folder, branch, maxFiles, ct)).Files.ToHashSet(StringComparer.Ordinal);
        var absent = defaults.Render(target).Where(f => !held.Contains($"{folder}/{f.Name}"))
            .Select(f => new RepoFile($"{folder}/{f.Name}", f.Text)).ToList();
        if (absent.Count == 0) return (true, $"{repo.Name}:{folder}/ already holds the app template; nothing was written.");
        var written = await provider.WriteFilesToBranchAsync(branch, absent, $"poc app template for target {target.Name}", ct);
        return written.Written
            ? (true, $"The app template of target '{target.Name}' is commit {written.CommitSha} in {repo.Name}:{folder}/ ({absent.Count} file(s)).")
            : (false, $"The app template of target '{target.Name}' could not be written to {repo.Name}: {written.Error}");
    }
}
