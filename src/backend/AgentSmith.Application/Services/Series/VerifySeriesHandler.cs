using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Filing;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207d: the series check on the default branch — the cross-ticket re-check no single ticket's keystone
/// can make, after every ticket merged. The embedded series-verify-master reads and runs what the criteria need and
/// records each; the verdict is kept on the series, and the failures not followed up before become one follow-up the
/// run's own filing steps file into the series. A run with nothing to file leaves those steps nothing to do.
/// </summary>
public sealed class VerifySeriesHandler(ISeriesRunStore store, ISeriesCriteria criteria, IAgenticLoopRunner loop, IPromptCatalog prompts, TimeProvider clock)
    : ICommandHandler<VerifySeriesContext>
{
    public const string MasterName = "series-verify-master";

    public async Task<CommandResult> ExecuteAsync(VerifySeriesContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<string>(ContextKeys.SeriesRunId, out var id) || await store.GetAsync(id!, cancellationToken) is not { } record)
            return CommandResult.Fail($"A series-verify run is started with an existing series' ContextKeys.{nameof(ContextKeys.SeriesRunId)}.");
        var (list, unread) = await criteria.ReadAsync(record, context.Project, cancellationToken);
        var verdictTool = new SeriesVerdictTool(list);
        if (list.Count > 0) await CheckAsync(context, list, verdictTool, cancellationToken);
        var verdict = new SeriesVerdict(pipeline.TryGet<string>(ContextKeys.RunId, out var run) ? run! : string.Empty, clock.GetUtcNow(), verdictTool.Results())
        {
            Unread = unread,
        };
        var fresh = SeriesFollowUp.New(record, verdict);
        await store.UpdateAsync(record.Id, r => r with
        {
            Verdicts = [.. r.Verdicts, verdict], FollowedUp = [.. r.FollowedUp, .. fresh.Select(f => f.Criterion)],
        }, cancellationToken);
        if (fresh.Count > 0) Seed(pipeline, SeriesFollowUp.Request(record, fresh));
        return CommandResult.Ok($"{verdict.Results.Count - verdict.Failed.Count} of {verdict.Results.Count} criteria hold; {fresh.Count} new failure(s) to file.");
    }

    private async Task CheckAsync(VerifySeriesContext context, IReadOnlyList<SeriesCriterion> list, SeriesVerdictTool verdict, CancellationToken ct)
    {
        var prompt = new StringBuilder("The series' tickets all landed on the default branch, checked out here. Check each criterion:\n");
        for (var i = 0; i < list.Count; i++) prompt.Append($"{i + 1}. {list[i].Cited}\n");
        var request = new AgenticLoopRequest(context.Project.Agent, TaskType.Primary, prompts.Get(MasterName), prompt.ToString(),
            SeriesVerifySurface.Tools(context.Pipeline, verdict), Name: MasterName, Compaction: context.Project.Agent.Compaction);
        var result = await loop.RunAsync(request, ct);
        PipelineCostTracker.GetOrCreate(context.Pipeline).Track(result.Response);
    }

    private static void Seed(PipelineContext pipeline, FilingRequest request)
    {
        pipeline.Set(ContextKeys.FilingRequest, request);
        pipeline.Set(ContextKeys.FilingReport, new FilingReportSlot());
    }
}
