using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Delivers findings via one or more IOutputStrategy implementations.
/// Resolves the output directory once — strategies receive it as a concrete path.
/// </summary>
public sealed class DeliverFindingsHandler(
    IServiceProvider serviceProvider,
    ILogger<DeliverFindingsHandler> logger) : ICommandHandler<DeliverFindingsContext>
{
    public async Task<CommandResult> ExecuteAsync(
        DeliverFindingsContext context, CancellationToken cancellationToken)
    {
        var outputDir = ResolveOutputDir(context.OutputDir);

        context.Pipeline.TryGet<List<SkillObservation>>(
            ContextKeys.SkillObservations, out var observations);

        var outputContext = new OutputContext(
            "api-scan", null,
            (IReadOnlyList<SkillObservation>)(observations ?? []),
            null, outputDir, context.Pipeline);

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

    internal static string ResolveOutputDir(string? requested)
    {
        // Try requested path first, then /output (Docker), then local fallback
        foreach (var candidate in new[] { requested, "/output", "./agentsmith-output" })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;

            try
            {
                Directory.CreateDirectory(candidate);
                var testFile = Path.Combine(candidate, ".write-test");
                File.WriteAllText(testFile, "");
                File.Delete(testFile);
                return candidate;
            }
            catch { /* not writable, try next */ }
        }

        // Last resort — temp directory is always writable
        return Path.GetTempPath();
    }
}
