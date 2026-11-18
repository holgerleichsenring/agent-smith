using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.Acts;

namespace AgentSmith.Server.Services.Admin.Secrets;

/// <summary>
/// 2026-11-17-f3a8d: a secret run through <see cref="IActRun"/> — in process, never queued, so the value travels in
/// memory only; the run records who changed which secret and when, and never what to.
/// </summary>
public sealed class SecretWriteRun(IActRun runs) : ISecretWriteRun
{
    public async Task<SecretWriteReport> RunAsync(SecretWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var slot = new ActSlot<SecretWriteReport>();
        var result = await runs.RunAsync(InstallationScope.Name, PipelinePresets.SecretName, new Dictionary<string, object>
        {
            [ContextKeys.SecretWriteRequest] = request,
            [ContextKeys.SecretWriteReport] = slot,
        });
        return slot.IsSet ? slot.Value! : new SecretWriteReport(false, result.Message ?? "The secret run ended before it wrote anything.");
    }
}
