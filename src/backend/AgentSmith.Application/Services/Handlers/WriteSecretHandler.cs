using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Admin;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-f3a8d: the secret run's one step. The request leaves the run's context as soon as it is read, so no
/// later step, checkpoint or failure report holds the value; a write reloads the configuration on this replica — the
/// runs and the masker then read the same values — and tells the others.
/// </summary>
public sealed class WriteSecretHandler(SecretWrite write, IConfigStore store, ConfigChangedSignal signal)
    : ICommandHandler<WriteSecretContext>
{
    public async Task<CommandResult> ExecuteAsync(WriteSecretContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<SecretWriteRequest>(ContextKeys.SecretWriteRequest, out var request) || request is null)
            return CommandResult.Fail("A secret run has no request — only the admin area's secret field starts one.");
        pipeline.Remove(ContextKeys.SecretWriteRequest);
        var runId = pipeline.TryGet<string>(ContextKeys.RunId, out var id) ? id ?? string.Empty : string.Empty;
        var report = await write.RunAsync(request, runId, cancellationToken);
        if (report.Written)
        {
            store.Load();
            await signal.SignalAsync("secret-store", request.Person);
        }
        if (pipeline.TryGet<ActSlot<SecretWriteReport>>(ContextKeys.SecretWriteReport, out var slot)) slot?.Set(report);
        return report.Written ? CommandResult.Ok(report.Line) : CommandResult.Fail(report.Line);
    }
}
