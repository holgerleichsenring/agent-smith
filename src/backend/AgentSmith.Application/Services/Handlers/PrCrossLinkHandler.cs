using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Pass-2 of the PR cross-linking flow. After CommitAndPR / InitCommit have
/// opened one PR per repo with a sibling-PRs marker in the body, this handler
/// replaces the marker in each opened PR with the actual sibling URL list via
/// the source provider's UpdatePullRequestBodyAsync. Single-PR runs short-
/// circuit (no PATCH issued). Patch failures are logged but do not fail the
/// pipeline — the PR is the load-bearing artifact and a missing cross-link is
/// a cosmetic loss.
/// </summary>
public sealed class PrCrossLinkHandler(
    ISourceProviderFactory sourceFactory,
    ILogger<PrCrossLinkHandler> logger) : ICommandHandler<PrCrossLinkContext>
{
    private const string SiblingMarker = "<!-- agentsmith:sibling-prs -->";

    public async Task<CommandResult> ExecuteAsync(
        PrCrossLinkContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<IReadOnlyList<OpenedPullRequest>>(
                ContextKeys.OpenedPullRequests, out var opened) || opened is null)
            return CommandResult.Ok("No OpenedPullRequests in context; nothing to cross-link.");

        var openedEntries = opened.Where(o => o.Status == OpenStatus.Opened && o.Url is not null).ToList();
        if (openedEntries.Count < 2)
        {
            logger.LogInformation("Cross-link skipped ({Count} opened PR — needs >= 2)", openedEntries.Count);
            return CommandResult.Ok("Cross-link skipped (single PR run).");
        }

        if (!context.Pipeline.TryGet<IReadOnlyDictionary<string, string>>(
                ContextKeys.OpenedPullRequestBodies, out var bodies) || bodies is null)
            return CommandResult.Fail("OpenedPullRequestBodies missing; cannot replace marker.");

        var siblings = RenderSiblingBlock(opened);
        var patched = await PatchAllAsync(context, openedEntries, bodies, siblings, cancellationToken);
        return CommandResult.Ok($"Cross-linked {patched}/{openedEntries.Count} PRs.");
    }

    private async Task<int> PatchAllAsync(
        PrCrossLinkContext context, IReadOnlyList<OpenedPullRequest> openedEntries,
        IReadOnlyDictionary<string, string> bodies, string siblings, CancellationToken ct)
    {
        var patched = 0;
        foreach (var entry in openedEntries)
        {
            var repo = context.Configs.FirstOrDefault(r => r.Name == entry.RepoName);
            if (repo is null)
            {
                logger.LogWarning("{Repo}: not found in Configs; skip", entry.RepoName);
                continue;
            }
            if (!bodies.TryGetValue(entry.RepoName, out var body))
            {
                logger.LogWarning("{Repo}: no body recorded; skip", entry.RepoName);
                continue;
            }
            var updated = body.Replace(SiblingMarker, siblings, StringComparison.Ordinal);
            var provider = sourceFactory.Create(repo);
            if (await provider.UpdatePullRequestBodyAsync(entry.Url!, updated, ct))
                patched++;
        }
        return patched;
    }

    private static string RenderSiblingBlock(IReadOnlyList<OpenedPullRequest> opened)
    {
        var lines = new List<string> { "### Related PRs" };
        foreach (var entry in opened)
            lines.Add(entry.Status switch
            {
                OpenStatus.Opened => $"- {entry.RepoName}: {entry.Url}",
                OpenStatus.Failed => $"- {entry.RepoName}: (open failed)",
                _ => $"- {entry.RepoName}: (no changes)"
            });
        return string.Join("\n", lines);
    }
}
