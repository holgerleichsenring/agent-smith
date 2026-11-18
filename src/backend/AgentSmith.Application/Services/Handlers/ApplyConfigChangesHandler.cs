using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Admin;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-f3a8c: the config run's one step — applies the approved changes for the person who pressed Apply,
/// signals every replica when anything was written, and leaves the report in the server's slot.
/// </summary>
public sealed class ApplyConfigChangesHandler(ConfigChangeApplier applier, ConfigChangedSignal signal)
    : ICommandHandler<ApplyConfigChangesContext>
{
    public async Task<CommandResult> ExecuteAsync(ApplyConfigChangesContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<ConfigChangeRequest>(ContextKeys.ConfigChangeRequest, out var request) || request is null)
            return CommandResult.Fail("A config run has no request — only an approved admin draft starts one.");
        var runId = pipeline.TryGet<string>(ContextKeys.RunId, out var id) ? id ?? string.Empty : string.Empty;
        var report = applier.Apply(request, runId);
        await signal.SignalAsync("admin-assistant", request.Person);
        if (pipeline.TryGet<ActSlot<ConfigApplyReport>>(ContextKeys.ConfigApplyReport, out var slot)) slot?.Set(report);
        return report.Applied ? CommandResult.Ok(report.Line) : CommandResult.Fail(report.Line);
    }
}
