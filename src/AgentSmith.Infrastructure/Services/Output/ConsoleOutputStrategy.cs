using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Output;

/// <summary>
/// Writes findings summary to stdout. Default strategy for --output console.
/// </summary>
public sealed class ConsoleOutputStrategy(
    ILogger<ConsoleOutputStrategy> logger) : IOutputStrategy
{
    public string StrategyType => "console";

    public Task DeliverAsync(OutputContext context, CancellationToken cancellationToken = default)
    {
        if (context.Findings.Count == 0)
        {
            logger.LogInformation("No findings.");
            return Task.CompletedTask;
        }

        var high = context.Findings.Count(f => f.Severity.Equals("HIGH", StringComparison.OrdinalIgnoreCase));
        var medium = context.Findings.Count(f => f.Severity.Equals("MEDIUM", StringComparison.OrdinalIgnoreCase));
        var low = context.Findings.Count(f => f.Severity.Equals("LOW", StringComparison.OrdinalIgnoreCase));

        logger.LogInformation("Found {Total} issues ({High} HIGH, {Medium} MEDIUM, {Low} LOW)",
            context.Findings.Count, high, medium, low);

        foreach (var finding in context.Findings)
        {
            var icon = finding.Severity.ToUpperInvariant() switch
            {
                "HIGH" => "HIGH",
                "MEDIUM" => "MEDIUM",
                "LOW" => "LOW",
                _ => finding.Severity
            };

            logger.LogInformation("[{Severity}] {File}:{Line} — {Title}",
                icon, finding.File, finding.StartLine, finding.Title);
        }

        if (context.ReportMarkdown is not null)
            logger.LogDebug("Full report:\n{Report}", context.ReportMarkdown);

        return Task.CompletedTask;
    }
}
