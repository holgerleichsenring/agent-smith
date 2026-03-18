using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Output;

/// <summary>
/// Writes findings summary to stdout. Default strategy for --output console.
/// </summary>
public sealed class ConsoleOutputStrategy(
    ILogger<ConsoleOutputStrategy> logger) : IOutputStrategy
{
    public string ProviderType => "console";

    public Task DeliverAsync(OutputContext context, CancellationToken cancellationToken = default)
    {
        if (context.Findings.Count == 0)
        {
            logger.LogInformation("No findings.");
            return Task.CompletedTask;
        }

        var summary = FindingSummary.From(context.Findings);

        logger.LogInformation("Found {Total} issues ({High} HIGH, {Medium} MEDIUM, {Low} LOW)",
            summary.Total, summary.High, summary.Medium, summary.Low);

        foreach (var finding in context.Findings)
        {
            logger.LogInformation("[{Severity}] {File}:{Line} — {Title}",
                finding.Severity.ToUpperInvariant(), finding.File, finding.StartLine, finding.Title);
        }

        if (context.ReportMarkdown is not null)
            logger.LogDebug("Full report:\n{Report}", context.ReportMarkdown);

        return Task.CompletedTask;
    }
}
