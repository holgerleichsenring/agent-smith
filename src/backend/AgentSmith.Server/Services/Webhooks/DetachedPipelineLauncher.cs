using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-10b0: starts a pull-request run — pr-review or a label scan — that nobody waits for,
/// from a webhook or the PR sweep alike. The request names the project and pipeline outright, so
/// there is no model call to parse them and no config path guessed; the run is ticketless, so it
/// takes no lease on a pull-request number. The scope lives as long as the run.
/// </summary>
public sealed class DetachedPipelineLauncher(
    IServiceScopeFactory scopes, ServerContext serverContext, ILogger<DetachedPipelineLauncher> logger) : IDetachedPipelineLauncher
{
    /// <summary>The request a detached run starts from: no ticket, so no lease; headless.</summary>
    public static PipelineRequest RequestFor(string project, string pipeline, Dictionary<string, object>? context) =>
        new(project, pipeline, TicketId: null, Headless: true, Context: context);

    public Task LaunchAsync(string project, string pipeline, Dictionary<string, object>? context) =>
        Task.Run(async () =>
        {
            await using var scope = scopes.CreateAsyncScope();
            try
            {
                var result = await scope.ServiceProvider.GetRequiredService<ExecutePipelineUseCase>().ExecuteAsync(
                    RequestFor(project, pipeline, context),
                    serverContext.ConfigPath, CancellationToken.None);
                logger.LogInformation(result.IsSuccess ? "Run {Pipeline} for {Project} completed: {Message}"
                    : "Run {Pipeline} for {Project} failed: {Message}", pipeline, project, result.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Run {Pipeline} for {Project} failed to start", pipeline, project);
            }
        });
}
