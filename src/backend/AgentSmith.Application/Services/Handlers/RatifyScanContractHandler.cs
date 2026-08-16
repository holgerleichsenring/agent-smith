using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Scans;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0429: states what the scan is looking for BEFORE the first scanner runs.
/// <para>
/// It sits early on purpose. A contract written after the findings are in is not a
/// contract — it is a description of what happened, and it can never report a miss.
/// </para>
/// </summary>
public sealed class RatifyScanContractHandler(
    IScanContractCatalogue catalogue,
    ILogger<RatifyScanContractHandler> logger)
    : ICommandHandler<RatifyScanContractContext>
{
    public Task<CommandResult> ExecuteAsync(
        RatifyScanContractContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var pipelineName = context.Pipeline.TryGet<string>(ContextKeys.PipelineName, out var name)
            ? name
            : null;

        var contract = catalogue.For(pipelineName);
        if (contract.Criteria.Count == 0)
        {
            logger.LogInformation(
                "No scan contract for pipeline '{Pipeline}' — nothing is claimed, so nothing is judged",
                pipelineName ?? "(unnamed)");
            return Task.FromResult(CommandResult.Ok("No scan contract for this pipeline"));
        }

        context.Pipeline.Set(ContextKeys.ScanContract, contract);
        foreach (var criterion in contract.Criteria)
            logger.LogInformation("Scan contract — {Criterion} (answered by {Step})",
                criterion.Statement, criterion.AnsweredBy);
        return Task.FromResult(CommandResult.Ok(
            $"Ratified {contract.Criteria.Count} scan criteria for '{pipelineName}'"));
    }
}
