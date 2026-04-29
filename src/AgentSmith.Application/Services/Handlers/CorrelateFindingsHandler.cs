using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Bridges the dynamic scanners (Nuclei, ZAP) with the static route map from
/// ApiCodeContext. Result is a list of FindingHandlerCorrelation rows that
/// downstream skills and output strategies use to render endpoint + file:line
/// for actionable findings.
/// </summary>
public sealed class CorrelateFindingsHandler(
    IFindingHandlerCorrelator correlator,
    ILogger<CorrelateFindingsHandler> logger)
    : ICommandHandler<CorrelateFindingsContext>
{
    public Task<CommandResult> ExecuteAsync(
        CorrelateFindingsContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        pipeline.TryGet<NucleiResult>(ContextKeys.NucleiResult, out var nuclei);
        pipeline.TryGet<ZapResult>(ContextKeys.ZapResult, out var zap);
        pipeline.TryGet<ApiCodeContext>(ContextKeys.ApiCodeContext, out var code);

        var correlations = correlator.Correlate(nuclei, zap, code);
        pipeline.Set(ContextKeys.FindingHandlerCorrelations, correlations);

        var matched = correlations.Count(c => c.Handler is not null);
        logger.LogInformation(
            "Correlated {Matched}/{Total} findings to handlers", matched, correlations.Count);
        return Task.FromResult(CommandResult.Ok($"Correlated {matched}/{correlations.Count} findings"));
    }
}
