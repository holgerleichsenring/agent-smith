using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Decides whether a master that fell silent gets re-engaged, and whether its
/// acceptance claim stands on its own. Pure predicates over the loop result,
/// verification and ledger; p0403 lifted them out of the handler.
/// </summary>
internal static class MasterReengagementPolicy
{
    // p0255: re-prompt the master to APPLY when the run expects edited source
    // (fix-bug / add-feature; not mad-discussion / scans) but it wrote only
    // run-record artifacts — a plan with zero source edits. Pure + testable.
    internal static bool ShouldDriveApply(string? pipelineName, IReadOnlyList<CodeChange> changes) =>
        !string.IsNullOrEmpty(pipelineName)
        && PipelinePresets.ExpectsCodeChanges(pipelineName)
        && !changes.Any(c => !RunRecordPaths.IsRunRecordPath(c.Path.ToString()));

    // 2026-11-22-96c8a: a spec's coding master whose passes ended on no text at all is asked once for its report — the
    // place the p0263 verdict nudge had. Then the run decides as always.
    internal static bool ShouldAskForReport(string? pipelineName, bool specExecution, bool hasReport) =>
        !hasReport && specExecution && !string.IsNullOrEmpty(pipelineName) && PipelinePresets.ExpectsCodeChanges(pipelineName);

    // p0341e: the ratified acceptance criteria for this run (empty when nothing was negotiated —
    // fix-bug self-planning, ticketless runs). Same source the keystone reads.
    // p0393a: the current SPEC's done-list, falling back to a ratified expectation for
    // pipelines that still negotiate one. Same source the keystone reads.
    internal static IReadOnlyList<string> RatifiedCriteria(PipelineContext pipeline) =>
        Specs.AcceptanceCriteria.For(pipeline);
}
