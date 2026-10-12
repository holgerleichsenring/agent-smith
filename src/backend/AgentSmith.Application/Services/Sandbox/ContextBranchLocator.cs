using AgentSmith.Application.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-10-12-6bfaa: decides which branch a pre-sandbox context read happens on. The first
/// candidate that exists on the remote AND lists something under <c>.agentsmith/contexts</c>
/// wins; one that is absent, empty or unreadable is passed over with its reason; with none left
/// the default branch answers, exactly as every read did before.
/// <para>
/// "Declares something" and not merely "exists": an existing work branch gets its base merged in
/// at checkout, so a branch without contexts sees the base's contexts once the run is on it.
/// </para>
/// </summary>
public sealed class ContextBranchLocator(ILogger<ContextBranchLocator> logger)
{
    public const string ContextsRoot = ".agentsmith/contexts";

    /// <summary>The default branch's listing propagates its failure, as the provider contract does.</summary>
    public async Task<ContextDirectory> LocateAsync(
        ISourceProvider provider, ContextReadBranches branches, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(branches);
        var passedOver = new List<string>();
        foreach (var branch in branches.Candidates)
        {
            var children = await TryListAsync(provider, branch, passedOver, ct);
            if (children is { Count: > 0 }) return new ContextDirectory(branch, children, passedOver);
        }
        var onDefault = await provider.ListDirectoryAsync(ContextsRoot, ct);
        return new ContextDirectory(null, onDefault, passedOver);
    }

    private async Task<IReadOnlyList<string>?> TryListAsync(
        ISourceProvider provider, BranchName branch, List<string> passedOver, CancellationToken ct)
    {
        try
        {
            if (!await provider.BranchExistsAsync(branch, ct))
            {
                passedOver.Add($"branch '{branch.Value}' is not on the remote");
                return null;
            }
            var children = await provider.ListDirectoryAsync(ContextsRoot, branch, ct);
            if (children.Count == 0) passedOver.Add($"branch '{branch.Value}' has no {ContextsRoot}/");
            return children;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Discovery: reading {Path} on {Branch} failed; trying the next branch.",
                ContextsRoot, branch.Value);
            passedOver.Add($"branch '{branch.Value}' could not be read ({ex.Message})");
            return null;
        }
    }
}
