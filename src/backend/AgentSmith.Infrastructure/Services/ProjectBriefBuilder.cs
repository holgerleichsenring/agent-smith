using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using YamlDotNet.Serialization;

namespace AgentSmith.Infrastructure.Services;

/// <summary>
/// Renders a compact, LLM-friendly project brief from the loaded
/// .agentsmith/ artifacts. Drops state.done / behavior / integrations
/// from context.yaml — those describe phase history and runtime
/// triggers, not the code under review.
/// </summary>
public sealed class ProjectBriefBuilder : IProjectBriefBuilder
{
    private static readonly HashSet<string> RelevantContextKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "meta", "stack", "arch", "quality"
    };

    private readonly IDeserializer _deserializer = new DeserializerBuilder().Build();

    public string Build(PipelineContext pipeline)
    {
        var contextYaml = pipeline.TryGet<string>(ContextKeys.ProjectContext, out var c) ? c : null;
        // p0384: the per-repo dictionary is the only code-map surface.
        var repoCodeMaps = pipeline.TryGet<IReadOnlyDictionary<string, string>>(
            ContextKeys.RepoCodeMaps, out var m) ? m : null;
        var codingPrinciples = pipeline.TryGet<string>(ContextKeys.DomainRules, out var d) ? d : null;

        if (contextYaml is null && (repoCodeMaps is null || repoCodeMaps.Count == 0) && codingPrinciples is null)
            return "## Project Brief\nStack: unknown — review on source-snippets only.";

        var sb = new StringBuilder();
        sb.AppendLine("## Project Brief");
        AppendContext(sb, contextYaml);
        AppendCodeMaps(sb, repoCodeMaps);
        AppendCodingPrinciples(sb, codingPrinciples);
        return sb.ToString().TrimEnd();
    }

    private void AppendContext(StringBuilder sb, string? yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml)) return;

        Dictionary<object, object?>? parsed;
        try { parsed = _deserializer.Deserialize<Dictionary<object, object?>>(yaml); }
        catch { return; }
        if (parsed is null) return;

        foreach (var (key, value) in parsed)
        {
            var keyStr = key?.ToString();
            if (keyStr is null || !RelevantContextKeys.Contains(keyStr)) continue;
            sb.AppendLine();
            sb.AppendLine($"### {keyStr}");
            RenderNode(sb, value, indent: 0);
        }
    }

    // p0384: one fenced block per repo so the brief attributes structure to the
    // right codebase; a single-repo dictionary renders one block.
    private static void AppendCodeMaps(StringBuilder sb, IReadOnlyDictionary<string, string>? maps)
    {
        if (maps is null || maps.Count == 0) return;
        foreach (var (repoName, yaml) in maps)
        {
            if (string.IsNullOrWhiteSpace(yaml)) continue;
            sb.AppendLine();
            sb.AppendLine($"### code map — {repoName}");
            sb.AppendLine("```yaml");
            sb.AppendLine(yaml.TrimEnd());
            sb.AppendLine("```");
        }
    }

    private static void AppendCodingPrinciples(StringBuilder sb, string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        sb.AppendLine();
        sb.AppendLine("### coding principles");
        sb.AppendLine(content.TrimEnd());
    }

    private static void RenderNode(StringBuilder sb, object? node, int indent)
    {
        var pad = new string(' ', indent * 2);
        switch (node)
        {
            case Dictionary<object, object?> dict:
                foreach (var (k, v) in dict)
                {
                    if (IsScalar(v))
                    {
                        sb.AppendLine($"{pad}- {k}: {v}");
                    }
                    else
                    {
                        sb.AppendLine($"{pad}- {k}:");
                        RenderNode(sb, v, indent + 1);
                    }
                }
                break;
            case List<object?> list:
                foreach (var item in list) sb.AppendLine($"{pad}- {item}");
                break;
            default:
                if (node is not null) sb.AppendLine($"{pad}{node}");
                break;
        }
    }

    private static bool IsScalar(object? v) =>
        v is null or string or bool or int or long or double or decimal;
}
