using AgentSmith.Application.Services.Admin;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Server.Services.Admin.Read;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Admin;

/// <summary>
/// 2026-11-17-f3a8b: the admin assistant's tools. They read through a unit of work of their own — the turn's is shared
/// with whatever else the run writes — held for the turn and released with it. Each result is masked against every
/// configured credential and cut at dialogue.admin_result_max_chars, in that order, so a cut never splits a value the
/// masker would have caught.
/// </summary>
public sealed class AdminToolSurface(IServiceScopeFactory scopes, SecretMasker masker, AdminReadSettings settings)
    : IAdminToolSurface, IAsyncDisposable
{
    private AsyncServiceScope? _scope;

    public IList<AITool> Tools(PipelineContext pipeline)
    {
        _scope ??= scopes.CreateAsyncScope();
        var services = _scope.Value.ServiceProvider;
        var tools = Read(services).Concat(Config.AdminConfigTools.For(pipeline, services));
        return [.. tools.Select(tool => new BoundedResultTool(new MaskedResultTool(tool, masker), settings.Chars))];
    }

    public ValueTask DisposeAsync() => _scope?.DisposeAsync() ?? ValueTask.CompletedTask;

    private static IEnumerable<AIFunction> Read(IServiceProvider services)
    {
        var runs = services.GetRequiredService<AdminRunTools>();
        var config = services.GetRequiredService<AdminConfigReadTools>();
        return
        [
            AIFunctionFactory.Create(runs.ListRuns, "list_runs"), AIFunctionFactory.Create(runs.ReadRun, "read_run"),
            AIFunctionFactory.Create(services.GetRequiredService<AdminCallTools>().ReadLlmCalls, "read_llm_calls"),
            AIFunctionFactory.Create(services.GetRequiredService<AdminEventTools>().ReadRunEvents, "read_run_events"),
            AIFunctionFactory.Create(services.GetRequiredService<AdminPromptTools>().ReadPrompt, "read_prompt"),
            AIFunctionFactory.Create(config.ReadConfig, "read_config"), AIFunctionFactory.Create(config.ReadSecrets, "read_secrets"),
            AIFunctionFactory.Create(services.GetRequiredService<AdminHealthTools>().ReadHealth, "read_health"),
            .. Tickets(services),
        ];
    }

    // The ticket readers come with the dashboard API; a composition without it reads no ticket record.
    private static IEnumerable<AIFunction> Tickets(IServiceProvider services) =>
        services.GetService<Tickets.TicketDetailReader>() is null
            ? []
            : [AIFunctionFactory.Create(ActivatorUtilities.CreateInstance<AdminTicketTools>(services).ReadTicketRecords, "read_ticket_records")];
}
