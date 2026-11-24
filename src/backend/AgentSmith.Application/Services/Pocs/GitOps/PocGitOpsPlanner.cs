using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: one plan of a PoC's deploy folder at the branch head — the app template read, rendered
/// and planned against the folder. The commit asks it again from the new head when another commit landed first.</summary>
public sealed class PocGitOpsPlanner(PocTemplateRead template, PocAppRender render, PocFolderPlan folders)
{
    public async Task<(PocFolderChanges? Plan, IReadOnlyList<string> Skipped, string? Refusal)> PlanAsync(
        ISourceProvider provider, BranchName branch, PocRenderInput input, string folder, int maxFiles, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var (read, unread) = await template.ReadAsync(provider, branch, input.Target, maxFiles, ct);
        if (read is null) return (null, [], unread);
        var (files, refused) = render.Render(read, input);
        if (files is null) return (null, read.Skipped, refused);
        var (plan, unplanned) = await folders.PlanAsync(provider, branch, folder, files, input.Seal, maxFiles, ct);
        return (plan, read.Skipped, unplanned);
    }
}
