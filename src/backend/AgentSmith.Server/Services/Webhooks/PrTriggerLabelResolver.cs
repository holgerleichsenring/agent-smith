using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-09-25-d83b: decides whether the labels on a pull request ask for a review, and names
/// the project the pull request's repo belongs to. The word used to be a literal compiled into
/// the GitHub and GitLab PR-label handlers, on the operator's own pull-request board, while
/// every other way of asking this framework for work is configured — it now comes from the
/// owning project's platform trigger (pr_trigger_label). The historical word never stops
/// triggering, so a deployment that configures nothing behaves exactly as it did.
/// </summary>
public sealed class PrTriggerLabelResolver
{
    /// <summary>The only word the two PR-label handlers matched before it was configurable.
    /// Read for ever: pull requests carrying it sit on boards nobody will relabel.</summary>
    public const string HistoricalLabel = "security-review";

    public PrTriggerMatch? Match(
        AgentSmithConfig config, string platformKind, string repoUrl,
        IEnumerable<string?> labels)
    {
        var present = labels.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l!).ToList();
        var owner = FindOwningProject(config, repoUrl);
        var configured = owner is null
            ? null
            : TriggerSelectionHelper.ByKind(owner.Value.Project, platformKind)?.PrTriggerLabel;

        return Asks(present, HistoricalLabel) || Asks(present, configured)
            ? new PrTriggerMatch(owner?.Name)
            : null;
    }

    private static bool Asks(IReadOnlyList<string> labels, string? word) =>
        !string.IsNullOrWhiteSpace(word)
        && labels.Contains(word, StringComparer.OrdinalIgnoreCase);

    /// <summary>The first project holding a repo whose configured URL the payload URL
    /// contains — the match the GitLab handler made for its result's project name, kept
    /// here so ONE reading of the config answers both which word triggers and which
    /// project the run belongs to.</summary>
    private static (string Name, ResolvedProject Project)? FindOwningProject(
        AgentSmithConfig config, string repoUrl)
    {
        foreach (var (name, project) in config.Projects)
            foreach (var repo in project.Repos)
                if (repo.Url is not null
                    && repoUrl.Contains(repo.Url, StringComparison.OrdinalIgnoreCase))
                    return (name, project);
        return null;
    }
}
