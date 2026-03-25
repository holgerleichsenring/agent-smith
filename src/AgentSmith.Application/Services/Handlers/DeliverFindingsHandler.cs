using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Delivers findings via one or more IOutputStrategy implementations.
/// Supports comma-separated --output values and configurable --output-dir.
/// </summary>
public sealed class DeliverFindingsHandler(
    IServiceProvider serviceProvider,
    ILogger<DeliverFindingsHandler> logger) : ICommandHandler<DeliverFindingsContext>
{
    public async Task<CommandResult> ExecuteAsync(
        DeliverFindingsContext context, CancellationToken cancellationToken)
    {
        var outputContext = new OutputContext(
            "api-scan",
            null,
            [],
            null,
            context.Pipeline);

        // Pass output dir to strategies via pipeline context
        if (context.OutputDir is not null)
            context.Pipeline.Set(ContextKeys.OutputDir, context.OutputDir);

        var delivered = new List<string>();

        foreach (var format in context.OutputFormats)
        {
            var strategy = serviceProvider.GetKeyedService<IOutputStrategy>(format);
            if (strategy is null)
            {
                logger.LogWarning("Unknown output format: '{Format}', skipping", format);
                continue;
            }

            await strategy.DeliverAsync(outputContext, cancellationToken);
            delivered.Add(format);
            logger.LogInformation("Delivered findings via {Format} strategy", format);
        }

        if (delivered.Count == 0)
            return CommandResult.Fail(
                $"No valid output formats found in: {string.Join(",", context.OutputFormats)}");

        return CommandResult.Ok($"Delivered via {string.Join(", ", delivered)}");
    }
}
