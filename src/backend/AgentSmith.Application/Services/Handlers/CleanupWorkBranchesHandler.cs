using AgentSmith.Contracts.Commands;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Branches;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-713ec: removes, per repository, the agent-smith/ branches <see cref="WorkBranchVerdict"/> says are
/// finished. A repository whose host cannot list branches is skipped and said so; one that fails is reported in its
/// line and the next is still swept; the step fails only when every repository it could sweep failed.
/// </summary>
public sealed class CleanupWorkBranchesHandler(
    ISourceProviderFactory sources, ILogger<CleanupWorkBranchesHandler> logger)
    : ICommandHandler<CleanupWorkBranchesContext>
{
    public async Task<CommandResult> ExecuteAsync(CleanupWorkBranchesContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var lines = new List<string>();
        int swept = 0, failed = 0;
        foreach (var repo in context.Repos)
        {
            if (sources.Create(repo) is not IWorkBranchCleanup host)
            {
                lines.Add($"{repo.Name}: its host lists no branches, skipped");
                continue;
            }
            swept++;
            try { lines.Add($"{repo.Name}: {await SweepAsync(host, cancellationToken)}"); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                logger.LogWarning(ex, "{Repo}: branch cleanup failed", repo.Name);
                lines.Add($"{repo.Name}: failed — {ex.Message}");
            }
        }
        var message = lines.Count == 0 ? "No repository to sweep." : string.Join("; ", lines);
        return swept > 0 && failed == swept ? CommandResult.Fail(message) : CommandResult.Ok(message);
    }

    private async Task<string> SweepAsync(IWorkBranchCleanup host, CancellationToken ct)
    {
        var deleted = new List<string>();
        var kept = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var tip in await host.ListWorkBranchesAsync(ct))
        {
            var reason = WorkBranchVerdict.KeepBecause(tip, await host.PullRequestsOfAsync(tip.Name, ct));
            if (reason is null && await host.DeleteBranchAsync(tip, ct))
            {
                deleted.Add(tip.Name);
                logger.LogInformation("Deleted finished branch {Branch}", tip.Name);
                continue;
            }
            reason ??= "the host refused the delete";
            kept[reason] = kept.GetValueOrDefault(reason) + 1;
        }
        var said = deleted.Count == 0 ? "deleted none" : $"deleted {string.Join(", ", deleted)}";
        return kept.Count == 0 ? said : $"{said}; kept {string.Join(", ", kept.Select(k => $"{k.Value} ({k.Key})"))}";
    }
}
