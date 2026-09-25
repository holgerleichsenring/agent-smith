using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// 2026-09-25-3c7ad: every pipeline name a ROUTING RULE names — a tracker's or a project's
/// label map, and either one's default — checked against what this product offers.
/// <para>
/// Nothing checked these before: the validator covers the global pipeline-trigger map and a
/// project's own pipelines list, and the normalizer says out loud that a trigger's label values
/// may route to any system pipeline. So the field an operator's live configuration carries a
/// retired name in is the one field nobody was reading.
/// </para>
/// <para>
/// ADVISORY, and that is the whole reason this is a separate check rather than a line in the
/// validator. A blocking finding covering a project disables every one of its triggers, so making
/// a typo in one label rule blocking would take a running deployment off the air. It is reported,
/// it names what the value could have been, and the run that would have used it fails exactly
/// where it always did.
/// </para>
/// </summary>
public static class RoutingPipelineNames
{
    public static IEnumerable<StartupFinding> Findings(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        foreach (var (name, project) in config.Projects)
            foreach (var finding in Unknown(Named(project), $"project '{name}'"))
                yield return finding;
    }

    private static IEnumerable<string> Named(ResolvedProject project) =>
        Triggers(project)
            .SelectMany(t => (t.PipelineFromLabel ?? new Dictionary<string, string>()).Values
                .Append(t.DefaultPipeline ?? string.Empty))
            .Append(project.DefaultPipeline ?? string.Empty);

    private static IEnumerable<WebhookTriggerConfig> Triggers(ResolvedProject project) =>
        new WebhookTriggerConfig?[]
            {
                project.JiraTrigger, project.GithubTrigger,
                project.GitlabTrigger, project.AzuredevopsTrigger,
            }
            .Where(t => t is not null).Select(t => t!);

    private static IEnumerable<StartupFinding> Unknown(IEnumerable<string> named, string where) =>
        named
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Where(n => !PipelinePresets.IsAcceptedName(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(n => new StartupFinding(
                StartupSubsystems.Configuration, StartupFindingSeverity.Advisory,
                $"A routing rule on {where} names pipeline '{n}', which this product does not "
                + Instead(n) + " A ticket routed to it will fail when it starts."));

    // 2026-09-25-e5b1: a name the collapse retired is the case an operator is most likely to be
    // holding, and "offered: code, security-scan, …" leaves them to guess which of those their
    // old word became. When we know, we say it; otherwise the offer is the best answer there is.
    private static string Instead(string named) =>
        RetiredPipelineNames.ReplacementFor(named) is { } target
            ? $"offer any more — it was retired into '{target}'. Write '{target}' instead."
            : $"offer (offered: {string.Join(", ", PipelinePresets.Routable)}).";
}
