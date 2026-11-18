using AgentSmith.Application.Services.Admin;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Admin.Config;

/// <summary>2026-11-17-f3a8c: the draft tools of a turn that carries a configuration draft; a turn without one gets none.</summary>
internal static class AdminConfigTools
{
    internal static IEnumerable<AIFunction> For(PipelineContext pipeline, IServiceProvider services)
    {
        if (!pipeline.TryGet<AdminConfigDraft>(ContextKeys.AdminConfigDraft, out var draft) || draft is null) return [];
        var tools = new AdminConfigDraftTools(services.GetRequiredService<IConfigStore>(),
            services.GetRequiredService<ConfigEntityKinds>(), draft,
            services.GetRequiredService<ILoggerFactory>().CreateLogger<AdminConfigDraftTools>());
        return
        [
            AIFunctionFactory.Create(tools.ReadConfigEntity, "read_config_entity"),
            AIFunctionFactory.Create(tools.DraftConfigChange, "draft_config_change"),
            AIFunctionFactory.Create(tools.DropConfigChange, "drop_config_change"),
        ];
    }
}
