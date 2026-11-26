namespace AgentSmith.Contracts.Runs;

/// <summary>2026-11-26-a12ed: a run's own report — each spec master's markdown as the run page shows it, read from the run row's
/// acceptance snapshot; null when the run left none.</summary>
public interface IRunReportReader
{
    Task<string?> ReportAsync(string runId, CancellationToken cancellationToken);
}

/// <summary>2026-11-26-a12ed: a run report as one text, and its short form for a wake — its headings.</summary>
public static class RunReportText
{
    /// <summary>The reports of an <see cref="AcceptanceView"/>: one alone as written, several each under its spec id.</summary>
    public static string? Of(AcceptanceView? view) => view?.Reports is not { Count: > 0 } reports ? null
        : reports.Count == 1 ? reports[0].Markdown
        : string.Join("\n\n", reports.Select(r => $"## Spec {r.SpecId ?? "(unnamed)"}\n\n{r.Markdown}"));

    /// <summary>The report's headings joined, or its first line when it has none; null for no report.</summary>
    public static string? Headings(string? report)
    {
        if (string.IsNullOrWhiteSpace(report)) return null;
        var lines = report.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        var headings = lines.Where(l => l.StartsWith('#')).Select(l => l.TrimStart('#').Trim()).Where(l => l.Length > 0).ToList();
        return headings.Count > 0 ? string.Join(" · ", headings) : lines[0];
    }
}
