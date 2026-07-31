using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Adds the legacy single-string Pipeline to the Pipelines list (if not already
/// declared) so it acts as a default that pipeline_from_label can route to, then
/// evaluates the project's configuration rules.
///
/// p0391a: the rules RECORD, they never throw. A project whose default_pipeline is
/// undeclared, or a trigger that cannot park, is a blocking finding that disables that
/// unit — the config still materializes, every other project keeps running, and the
/// server comes up to say what is wrong.
/// </summary>
public sealed class ProjectConfigNormalizer(
    ILogger<ProjectConfigNormalizer>? logger = null, IStartupFindings? findings = null)
{
    private const string DefaultProjectSkillsPath = "skills/coding";
    // Both optional so the loader's many test constructions can `new` it without DI; the
    // real logger and the shared findings list are injected in production.
    private readonly ILogger _logger = logger ?? NullLogger<ProjectConfigNormalizer>.Instance;
    private readonly IStartupFindings _findings = findings ?? new StartupFindings();

    public void Normalize(string projectName, RawProjectEntry project)
    {
        ApplyLegacyShim(project);
        foreach (var finding in Evaluate(projectName, project)) RecordAndLog(finding);
    }

    private static IEnumerable<StartupFinding> Evaluate(string projectName, RawProjectEntry project)
    {
        var pipeline = UndeclaredDefaultPipeline(projectName, project);
        if (pipeline is not null) yield return pipeline;
        foreach (var finding in ProjectTriggerRules.Evaluate(projectName, project))
            yield return finding;
    }

    private void RecordAndLog(StartupFinding finding)
    {
        _findings.Record(finding);
        if (finding.IsBlocking) _logger.LogError("Config: {Reason}", finding.Reason);
        else _logger.LogWarning("Config: {Reason}", finding.Reason);
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

    // Per-pipeline overrides go in Pipelines explicitly; pipelines without overrides
    // inherit project defaults via the resolver. Only the project-level DefaultPipeline
    // is checked — trigger label values may route to any system pipeline.
    private static StartupFinding? UndeclaredDefaultPipeline(
        string projectName, RawProjectEntry project)
    {
        if (project.DefaultPipeline is null) return null;
        if (project.Pipelines.Any(p => string.Equals(
            p.Name, project.DefaultPipeline, StringComparison.OrdinalIgnoreCase))) return null;

        return new StartupFinding(
            StartupSubsystems.Configuration,
            StartupFindingSeverity.Blocking,
            $"Project '{projectName}': default_pipeline '{project.DefaultPipeline}' is not "
            + "declared in pipelines. This project is disabled until the pipeline is declared.",
            projectName, Field: "default_pipeline");
    }
}
