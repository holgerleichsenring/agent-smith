using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Webhooks;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-09-25-d83b: decides whether the labels on a pull request ask for a review, and names
/// the project the pull request's repo belongs to. The word used to be a literal compiled into
/// the GitHub and GitLab PR-label handlers, on the operator's own pull-request board, while
/// every other way of asking this framework for work is configured — it now comes from the
/// owning project's platform trigger (pr_trigger_label). The historical word never stops
/// triggering, so a deployment that configures nothing behaves exactly as it did.
/// </summary>
public sealed class PrTriggerLabelResolver(IConfiguredRepoFinder? repoFinder = null)
{
    private readonly IConfiguredRepoFinder _repos = repoFinder ?? new ConfiguredRepoFinder();

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

    /// <summary>The project holding a repo whose URL EQUALS the payload's (2026-10-08-10b0: host and path,
    /// as <see cref="IConfiguredRepoFinder"/> compares them — never a substring, which handed one
    /// repository's label to its prefix sibling).</summary>
    private (string Name, ResolvedProject Project)? FindOwningProject(AgentSmithConfig config, string repoUrl) =>
        _repos.Find(config, repoUrl) is { } owner ? (owner.ProjectName, owner.Project) : null;
}
