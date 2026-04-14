using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Builds upstream context for structured skill rounds based on role type.
/// Gate/Lead roles receive all upstream outputs. Executors receive the plan.
/// </summary>
public sealed class UpstreamContextBuilder : IUpstreamContextBuilder
{
    public string Build(
        SkillRole role,
        PipelineContext pipeline,
        Dictionary<string, string> skillOutputs)
    {
        return role switch
        {
            SkillRole.Gate => FormatOutputs(skillOutputs),
            SkillRole.Lead => FormatOutputs(skillOutputs),
            SkillRole.Executor => BuildExecutorContext(pipeline, skillOutputs),
            _ => string.Empty
        };
    }

    private static string FormatOutputs(Dictionary<string, string> skillOutputs)
    {
        if (skillOutputs.Count == 0) return "No upstream findings.";
        return string.Join("\n\n---\n\n",
            skillOutputs.Select(kv => $"### {kv.Key}\n{kv.Value}"));
    }

    private static string BuildExecutorContext(
        PipelineContext pipeline, Dictionary<string, string> skillOutputs)
    {
        pipeline.TryGet<string>(ContextKeys.ConsolidatedPlan, out var plan);
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(plan))
            sb.AppendLine($"## Plan\n{plan}\n");
        if (skillOutputs.Count > 0)
            sb.AppendLine($"## Prior Outputs\n{FormatOutputs(skillOutputs)}");
        return sb.ToString();
    }
}
