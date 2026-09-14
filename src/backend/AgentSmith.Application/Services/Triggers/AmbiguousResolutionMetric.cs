using AgentSmith.Application.Services.Metrics;
using AgentSmith.Contracts.Models.Triggers;

namespace AgentSmith.Application.Services.Triggers;

/// <summary>
/// 2026-09-13-a3f1: one ticket matching more than one project is worth counting, and
/// counting it is not a routing decision. Carved out of <see cref="ProjectResolver"/>,
/// which is over the file-length limit and may only get shorter.
/// </summary>
internal static class AmbiguousResolutionMetric
{
    public static void EmitIfAmbiguous(AgentSmithMetrics metrics, IReadOnlyList<ProjectMatch> matches)
    {
        if (matches.Count <= 1) return;
        foreach (var match in matches)
            metrics.AmbiguousResolution.Add(1,
                new KeyValuePair<string, object?>("project", match.ProjectName),
                new KeyValuePair<string, object?>("pipeline", match.PipelineName));
    }
}
