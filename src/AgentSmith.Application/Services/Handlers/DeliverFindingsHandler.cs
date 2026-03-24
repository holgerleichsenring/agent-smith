using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Delivers findings via IOutputStrategy for repo-less pipelines.
/// No repository, no file archiving — strategy only.
/// </summary>
public sealed class DeliverFindingsHandler(
    IServiceProvider serviceProvider,
    ILogger<DeliverFindingsHandler> logger) : ICommandHandler<DeliverFindingsContext>
{
    public async Task<CommandResult> ExecuteAsync(
        DeliverFindingsContext context, CancellationToken cancellationToken)
    {
        var strategy = serviceProvider.GetKeyedService<IOutputStrategy>(context.OutputFormat);
        if (strategy is null)
            return CommandResult.Fail($"Unknown output format: '{context.OutputFormat}'");

        var outputContext = new OutputContext(
            "api-scan",
            null,
            [],
            null,
            context.Pipeline);

        await strategy.DeliverAsync(outputContext, cancellationToken);

        logger.LogInformation("Delivered findings via {Format} strategy", context.OutputFormat);
        return CommandResult.Ok($"Delivered via {context.OutputFormat}");
    }
}
