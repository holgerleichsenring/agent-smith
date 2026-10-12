using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Structure;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-12-fbbda: the units of ONE repository past their limit but inside the tolerance band,
/// in that repository's pull-request body — one line each, 'within tolerance: path key metric
/// value/limit'. Whether each is solid is the reviewer's call, not the framework's.
/// </summary>
public static class StructureDivergenceSection
{
    public const string Heading = "## Structure within tolerance";

    private const string Preamble =
        "These units are past their limit but inside the tolerance band the principles allow. "
        + "The gate let them through; the reviewer judges whether each one is solid.";

    public static string Build(PipelineContext pipeline, string? repository)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (repository is null
            || !pipeline.TryGet<IReadOnlyList<StructureFinding>>(ContextKeys.StructureDivergences, out var all)
            || all is null)
            return string.Empty;
        var own = all.Where(f => f.Repository == repository).ToList();
        if (own.Count == 0) return string.Empty;
        var lines = new List<string> { string.Empty, string.Empty, Heading, string.Empty, Preamble, string.Empty };
        lines.AddRange(own.Select(Row));
        return string.Join("\n", lines);
    }

    public static string Row(StructureFinding finding) =>
        $"- within tolerance: {finding.Path} {finding.Key} {finding.Metric} {finding.Value}/{finding.Limit}";
}
