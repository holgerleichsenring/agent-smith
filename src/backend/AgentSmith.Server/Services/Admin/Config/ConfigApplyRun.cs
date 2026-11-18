using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.Acts;

namespace AgentSmith.Server.Services.Admin.Config;

/// <summary>
/// 2026-11-17-f3a8c: a config run through <see cref="IActRun"/>, the entry every act's run shares — so applying
/// configuration has a run id, a trail and the change rows naming the person and the run.
/// </summary>
public sealed class ConfigApplyRun(IActRun runs) : IConfigApplyRun
{
    public async Task<ConfigApplyReport> RunAsync(ConfigChangeOutcome changes, string person, bool mayWriteSecrets)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var slot = new ActSlot<ConfigApplyReport>();
        var result = await runs.RunAsync(InstallationScope.Name, PipelinePresets.ConfigName, new Dictionary<string, object>
        {
            [ContextKeys.ConfigChangeRequest] = new ConfigChangeRequest(changes.Changes, person, mayWriteSecrets),
            [ContextKeys.ConfigApplyReport] = slot,
        });
        return slot.IsSet ? slot.Value! : new ConfigApplyReport(false, result.Message ?? "The config run ended before it applied anything.");
    }
}
