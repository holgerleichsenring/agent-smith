using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Adds the legacy single-string Pipeline to the Pipelines list (if not already
/// declared) so it acts as a default that pipeline_from_label can route to.
/// Per-pipeline overrides go in Pipelines explicitly; pipelines without
/// overrides inherit project defaults via the resolver. Validates only the
/// project-level DefaultPipeline against the declared list — trigger label
/// values are not pre-validated since they may route to any system pipeline.
/// </summary>
public sealed class ProjectConfigNormalizer(ILogger<ProjectConfigNormalizer>? logger = null)
{
    private const string DefaultProjectSkillsPath = "skills/coding";
    // Optional so the loader's many test constructions can `new` it without DI; the
    // real logger is injected in production. Only used for the p0261 empty-statuses warn.
    private readonly ILogger _logger = logger ?? NullLogger<ProjectConfigNormalizer>.Instance;

    public void Normalize(string projectName, RawProjectEntry project)
    {
        ApplyLegacyShim(project);
        ValidateDefaultPipeline(projectName, project);
        ValidateTriggerStatuses(projectName, project);
    }

    private static void ApplyLegacyShim(RawProjectEntry project)
    {
        if (string.IsNullOrEmpty(project.Pipeline)) return;
        if (!project.Pipelines.Any(p => string.Equals(
            p.Name, project.Pipeline, StringComparison.OrdinalIgnoreCase)))
        {
            project.Pipelines.Add(new RawPipelineEntry
            {
                Name = project.Pipeline,
                SkillsPath = NonDefaultSkillsPath(project),
                CodingPrinciplesPath = project.CodingPrinciplesPath,
            });
        }
        project.DefaultPipeline ??= project.Pipeline;
    }

    private static string? NonDefaultSkillsPath(RawProjectEntry project) =>
        string.Equals(project.SkillsPath, DefaultProjectSkillsPath, StringComparison.Ordinal)
            ? null
            : project.SkillsPath;

    private static void ValidateDefaultPipeline(string projectName, RawProjectEntry project)
    {
        if (project.DefaultPipeline is null) return;
        if (project.Pipelines.Any(p => string.Equals(
            p.Name, project.DefaultPipeline, StringComparison.OrdinalIgnoreCase))) return;

        throw new ConfigurationException(
            $"Project '{projectName}': default_pipeline '{project.DefaultPipeline}' " +
            $"is not declared in pipelines.");
    }

    // p0261: the trigger decision now rests on the NATIVE status (trigger_statuses) —
    // done_status/failed_status are no longer gated by a lifecycle tag. Two
    // misconfigurations would re-claim a just-processed ticket forever; catch both at
    // load. (1) A terminal status that is ALSO a trigger_status → a ticket terminalized
    // there stays claimable → loop: hard error. (2) Empty trigger_statuses → every
    // native status is accepted, so terminalization can't keep a ticket out → warn loud
    // (it is the base-config default, so a throw would break startup; the operator must
    // narrow it for the p0261 model to hold).
    private void ValidateTriggerStatuses(string projectName, RawProjectEntry project)
    {
        foreach (var (kind, trigger) in EnumerateTriggers(project))
        {
            if (trigger is null) continue;
            if (trigger.TriggerStatuses.Count == 0)
            {
                _logger.LogWarning(
                    "Project '{Project}' {Trigger}: trigger_statuses is empty — every native status is " +
                    "accepted, so a ticket terminalized to done_status/failed_status can be re-claimed " +
                    "immediately (p0261). Set trigger_statuses to the open states only.",
                    projectName, kind);
                continue;
            }
            FailIfTerminalIsTrigger(projectName, kind, "done_status", trigger.DoneStatus, trigger.TriggerStatuses);
            FailIfTerminalIsTrigger(projectName, kind, "failed_status", trigger.FailedStatus, trigger.TriggerStatuses);
        }
    }

    private static IEnumerable<(string Kind, WebhookTriggerConfig? Trigger)> EnumerateTriggers(RawProjectEntry p)
    {
        yield return ("jira_trigger", p.JiraTrigger);
        yield return ("github_trigger", p.GithubTrigger);
        yield return ("gitlab_trigger", p.GitlabTrigger);
        yield return ("azuredevops_trigger", p.AzuredevopsTrigger);
    }

    private static void FailIfTerminalIsTrigger(
        string projectName, string kind, string field, string? status, List<string> triggerStatuses)
    {
        if (string.IsNullOrEmpty(status)) return;
        if (triggerStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            throw new ConfigurationException(
                $"Project '{projectName}' {kind}: {field} '{status}' is also a trigger_status. " +
                $"A processed ticket would land back in a trigger status and be re-claimed immediately " +
                $"(loop). {field} must be OUTSIDE trigger_statuses.");
    }
}
