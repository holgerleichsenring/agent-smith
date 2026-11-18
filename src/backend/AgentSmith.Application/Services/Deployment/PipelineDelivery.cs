using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>
/// 2026-11-18-3863e: starts the pipeline on the repository's host and reads the started run once — pending while it
/// queues or runs, green, or red with its failed jobs and links. A host that runs no pipeline, or refuses the start, is
/// a failed deploy in the host's words; a run the host did not name, or a read that fails, is said and the deploy stands.
/// </summary>
public sealed class PipelineDelivery(IPipelineRunnerFactory runners, ILogger<PipelineDelivery> logger)
{
    /// <summary>Whether the pipeline started, the sentence that says where its run stands, and the run.</summary>
    public async Task<(bool Started, string Sentence, PipelineRunStart? Run)> DeliverAsync(PipelineTriggerOrder order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        var what = order.Run.Pipeline is { } name ? $"pipeline `{name}` of `{order.Repo.Name}`" : $"the pipeline of `{order.Repo.Name}`";
        if (runners.Create(order.Repo) is not { } runner)
            return (false, $"`{order.Repo.Name}` is a {order.Repo.Type} repository, whose host runs no pipeline", null);
        PipelineRunStart start;
        try { start = await runner.StartAsync(order.Run, cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The {What} could not be started", what);
            return (false, $"{what} could not be started: {ex.Message}", null);
        }
        if (start.RunId is null)
            return (true, $"{what} started; the host did not name the run, so it is not followed — its runs are at {start.Url}", start);
        return (true, $"{what} started run {start.Url}; {await StateAsync(runner, start, order.Ci, cancellationToken)}", start);
    }

    private async Task<string> StateAsync(IPipelineRunner runner, PipelineRunStart start, CiReadRequest bounds, CancellationToken ct)
    {
        try
        {
            var run = await runner.ReadAsync(start.RunId!, bounds, ct);
            return run.State == CiStates.Red ? "it failed: " + DeployCiStatus.Failed(run.Failures) : $"it is {Words(run.State)}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Pipeline run {Url} could not be read", start.Url);
            return $"its state could not be read ({ex.Message})";
        }
    }

    private static string Words(string state) => state == CiStates.Green ? "green" : "running";
}
