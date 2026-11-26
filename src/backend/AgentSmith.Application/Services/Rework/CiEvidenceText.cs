using System.Text;
using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Application.Services.Rework;

/// <summary>2026-11-17-3435c, recovered by 2026-11-26-49d3a: a red CI's evidence as text — per pull request and failed
/// check, the link and the excerpt the record stored. The series master's ci read and a run's prompt say it so.</summary>
public static class CiEvidenceText
{
    public static string Of(IEnumerable<CiFailureFeedback> red)
    {
        ArgumentNullException.ThrowIfNull(red);
        var sb = new StringBuilder();
        foreach (var pr in red)
            foreach (var failure in pr.Failures)
                sb.Append($"### {failure.Name} ({pr.Repo}, {pr.PrUrl})\n")
                    .Append(failure.Url is null ? string.Empty : $"Full log: {failure.Url}\n")
                    .Append("```\n").Append(failure.Excerpt.TrimEnd()).Append("\n```\n\n");
        return sb.ToString().TrimEnd();
    }
}
