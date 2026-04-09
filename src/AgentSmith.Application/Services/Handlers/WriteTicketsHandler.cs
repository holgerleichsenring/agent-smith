using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Creates tickets from autonomous findings via the ticket provider.
/// Respects MaxTickets and MinConfidence thresholds.
/// </summary>
public sealed class WriteTicketsHandler(
    ITicketProviderFactory ticketFactory,
    ILogger<WriteTicketsHandler> logger)
    : ICommandHandler<WriteTicketsContext>
{
    internal const string Label = "agent-smith-autonomous";

    public async Task<CommandResult> ExecuteAsync(
        WriteTicketsContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<IReadOnlyList<AutonomousFinding>>(
                ContextKeys.AutonomousFindings, out var findings) || findings is null || findings.Count == 0)
        {
            logger.LogInformation("No autonomous findings to write");
            return CommandResult.Ok("No findings to write as tickets");
        }

        var eligible = findings
            .Where(f => f.Confidence >= context.MinConfidence)
            .OrderByDescending(f => f.Confidence)
            .Take(context.MaxTickets)
            .ToList();

        if (eligible.Count == 0)
        {
            logger.LogInformation(
                "All {Total} findings below minimum confidence {Min}",
                findings.Count, context.MinConfidence);
            return CommandResult.Ok(
                $"All {findings.Count} finding(s) below minimum confidence {context.MinConfidence}");
        }

        var provider = ticketFactory.Create(context.TicketConfig);
        var writtenUrls = new List<string>();

        foreach (var finding in eligible)
        {
            var body = BuildTicketBody(finding);

            try
            {
                var ticketId = await provider.CreateAsync(
                    finding.Title, body, [Label], cancellationToken);

                var url = $"#{ticketId}";
                writtenUrls.Add(url);

                logger.LogInformation(
                    "Created ticket {TicketId} for finding: {Title} (confidence: {Confidence})",
                    ticketId, finding.Title, finding.Confidence);
            }
            catch (NotSupportedException)
            {
                logger.LogWarning(
                    "Ticket provider does not support CreateAsync — skipping ticket creation");
                return CommandResult.Ok("Ticket provider does not support ticket creation");
            }
        }

        context.Pipeline.Set(ContextKeys.WrittenTickets, writtenUrls.AsReadOnly());

        var summary = $"Created {writtenUrls.Count} ticket(s) from {findings.Count} finding(s)";
        logger.LogInformation("{Summary}", summary);
        return CommandResult.Ok(summary);
    }

    internal static string BuildTicketBody(AutonomousFinding finding)
    {
        var sb = new StringBuilder();
        sb.AppendLine(finding.Description);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"**Category:** {finding.Category}");
        sb.AppendLine($"**Confidence:** {finding.Confidence}/10");
        sb.AppendLine($"**Found by:** {finding.FoundByRole}");

        if (finding.AgreedByRoles.Count > 0)
            sb.AppendLine($"**Agreed by:** {string.Join(", ", finding.AgreedByRoles)}");

        sb.AppendLine();
        sb.AppendLine("_Created by Agent Smith autonomous pipeline_");

        return sb.ToString();
    }
}
