using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Commands;

/// <summary>
/// p0113: Hidden CLI subcommand used by Server-spawned containers to pick up a
/// queue-claimed PipelineRequest. Server saves the structured request under a
/// jobId via IPipelineRequestStore; this command loads it and drives the
/// pipeline via the structured ExecutePipelineUseCase overload — no string
/// round-tripping through the intent parser.
///
/// Reachable for debugging via:
///   agent-smith run-claimed-job --job-id &lt;id&gt; --redis-url &lt;url&gt; --config &lt;path&gt;
/// (hidden from --help).
/// </summary>
internal static class RunClaimedJobCommand
{
    public static Command Create(Option<string> configOption, Option<bool> verboseOption)
    {
        var jobIdOption = new Option<string>("--job-id", "Job id assigned by the dispatcher") { IsRequired = true };
        var redisUrlOption = new Option<string>("--redis-url", "Redis connection URL") { IsRequired = true };

        var cmd = new Command("run-claimed-job", "Internal: pick up a queue-claimed pipeline job (hidden)")
        {
            jobIdOption, redisUrlOption, configOption, verboseOption,
        };
        cmd.IsHidden = true;

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var jobId = ctx.ParseResult.GetValueForOption(jobIdOption)!;
            var redisUrl = ctx.ParseResult.GetValueForOption(redisUrlOption)!;
            var configPath = ctx.ParseResult.GetValueForOption(configOption)!;
            var verbose = ctx.ParseResult.GetValueForOption(verboseOption);

            var provider = ServiceProviderFactory.Build(
                verbose, headless: true, jobId: jobId, redisUrl: redisUrl, configPath: configPath);
            var logger = provider.GetRequiredService<ILogger<Program>>();

            var store = provider.GetRequiredService<IPipelineRequestStore>();
            var request = await store.LoadAsync(jobId, CancellationToken.None);
            if (request is null)
            {
                logger.LogError(
                    "PipelineRequest for job {JobId} not found in Redis (key missing or expired). " +
                    "Exiting non-zero so the orchestrator marks the job failed.",
                    jobId);
                ctx.ExitCode = 2;
                return;
            }

            logger.LogInformation(
                "Picked up job {JobId}: {Project}/#{Ticket} pipeline={Pipeline}",
                jobId, request.ProjectName, request.TicketId?.Value ?? "—", request.PipelineName);

            CommandResult result;
            try
            {
                var useCase = provider.GetRequiredService<ExecutePipelineUseCase>();
                result = await useCase.ExecuteAsync(request, configPath, CancellationToken.None);
            }
            catch (Exception ex)
            {
                result = CommandResult.Fail($"Unhandled exception: {ex.Message}");
                logger.LogError(ex, "Pipeline execution threw for job {JobId}", jobId);
            }

            if (result.IsSuccess)
                logger.LogInformation("Job {JobId} succeeded: {Message}", jobId, result.Message);
            else
                logger.LogWarning("Job {JobId} failed: {Message}", jobId, result.Message);

            ctx.ExitCode = result.IsSuccess ? 0 : 1;
        });

        return cmd;
    }
}
