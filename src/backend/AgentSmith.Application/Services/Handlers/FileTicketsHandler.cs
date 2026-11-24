using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-5701b: the second step of a 'file' run — files the plan and puts what it did into the report slot the
/// server reads back. Creating and starting are this one step, so no cancel between steps leaves a created root
/// unstarted. It fails only when nothing was created; once a ticket exists it returns Ok with the rest in the report.
/// </summary>
/// <para>2026-11-24-f9c5b: with the series store, the filing's series is opened or joined between creating and starting
/// (<see cref="Series.SeriesMembership"/>), and a follow-up filed into a series with a branch writes its ticket branch from
/// that branch.</para>
public sealed class FileTicketsHandler(FilingPlanFiler filer, Series.SeriesMembership? series = null) : ICommandHandler<FileTicketsContext>
{
    public async Task<CommandResult> ExecuteAsync(FileTicketsContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (!pipeline.Has(ContextKeys.FilingRequest) && pipeline.Has(ContextKeys.SeriesRunId)) // 2026-11-17-0207d
            return CommandResult.Ok("Nothing to file: the series check found no new failure.");
        if (!pipeline.TryGet<FilingRequest>(ContextKeys.FilingRequest, out var request) || request is null
            || !pipeline.TryGet<FilingPlan>(ContextKeys.FilingPlan, out var plan) || plan is null)
            return CommandResult.Fail("FileTickets has no filing request or plan — PlanFiling runs first. Nothing was filed.");
        if (!pipeline.TryGet<FilingReportSlot>(ContextKeys.FilingReport, out var slot) || slot is null)
            return CommandResult.Fail("A 'file' run needs the report slot the server seeds under "
                + $"ContextKeys.{nameof(ContextKeys.FilingReport)}; nothing was filed, because nobody could read what was.");
        if (series is not null && (await series.JoiningAsync(pipeline, cancellationToken))?.IntegrationBranch is { } branch)
            request = request with { SpecBranchFrom = new Domain.Models.BranchName(branch) };
        var report = await filer.FileAsync(request, context.Project, plan, cancellationToken, series is null ? null
            : filed => series.WriteAsync(pipeline, request, context.Project.Name, filed, CancellationToken.None));
        slot.Report = report;
        return report.Filed.Count == 0 && report.Error is { } error
            ? CommandResult.Fail(error)
            : CommandResult.Ok($"Filed {report.Filed.Count} of {plan.Tickets.Count} ticket(s)"
                + (report.Error is null ? string.Empty : $"; stopped: {report.Error}"));
    }
}
