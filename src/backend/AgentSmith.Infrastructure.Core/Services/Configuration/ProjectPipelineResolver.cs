using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-09-13-5fa0: the per-project pipeline list, carved out of
/// <see cref="ResolvedProjectBuilder"/> — which sat exactly at its file-length baseline and
/// so could not take the template resolution without giving something up first. The same
/// carve <see cref="ProjectRepoResolver"/> already is.
/// </summary>
internal static class ProjectPipelineResolver
{
    public static IReadOnlyList<PipelineDefinition>? Resolve(
        string project, IReadOnlyList<RawPipelineEntry> raws,
        IReadOnlyDictionary<string, AgentConfig> agents, List<StartupFinding> findings)
    {
        var result = new List<PipelineDefinition>(raws.Count);
        var anyError = false;
        foreach (var r in raws)
        {
            if (string.IsNullOrEmpty(r.Name))
            {
                findings.Add(ProjectFindings.Blocking(project, "pipelines",
                    $"Project '{project}': pipelines entry is missing required field 'name'."));
                anyError = true;
                continue;
            }

            AgentConfig? resolvedAgent = null;
            if (!string.IsNullOrEmpty(r.Agent) && !agents.TryGetValue(r.Agent, out resolvedAgent))
            {
                findings.Add(ProjectFindings.Blocking(project, "pipelines",
                    $"Project '{project}': pipeline '{r.Name}' references agent '{r.Agent}' " +
                    "which is not defined in agents: catalog."));
                anyError = true;
            }

            if (r.ConfidenceThreshold is < 0 or > 100)
            {
                findings.Add(ProjectFindings.Blocking(project, "pipelines",
                    $"Project '{project}': pipeline '{r.Name}' has confidence_threshold " +
                    $"{r.ConfidenceThreshold} — must be between 0 and 100."));
                anyError = true;
            }

            result.Add(new PipelineDefinition
            {
                Name = r.Name,
                AgentName = string.IsNullOrEmpty(r.Agent) ? null : r.Agent,
                Agent = resolvedAgent,
                SkillsPath = r.SkillsPath,
                CodingPrinciplesPath = r.CodingPrinciplesPath,
                ConfidenceThreshold = r.ConfidenceThreshold,
            });
        }
        return anyError ? null : result;
    }
}
