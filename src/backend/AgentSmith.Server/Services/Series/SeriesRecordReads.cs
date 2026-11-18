using AgentSmith.Application.Services.Series;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Runs;
using AgentSmith.Server.Services.Admin.Read;
using Microsoft.Extensions.AI;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-17-0207e: the admin assistant's curated run reads for the series master — read_run, read_llm_calls and
/// read_run_events — on a unit of work of their own, masked against every configured credential and cut at
/// dialogue.admin_result_max_chars exactly as the admin assistant gets them. One reader, two masters.
/// </summary>
public sealed class SeriesRecordReads(IServiceScopeFactory scopes, SecretMasker masker) : ISeriesRecordReads, IAsyncDisposable
{
    private AsyncServiceScope? _scope;

    public IList<AITool> Tools()
    {
        _scope ??= scopes.CreateAsyncScope();
        var services = _scope.Value.ServiceProvider;
        var runs = services.GetRequiredService<AdminRunTools>();
        var chars = services.GetRequiredService<AdminReadSettings>().Chars;
        AIFunction[] reads =
        [
            AIFunctionFactory.Create(runs.ReadRun, "read_run"),
            AIFunctionFactory.Create(services.GetRequiredService<AdminCallTools>().ReadLlmCalls, "read_llm_calls"),
            AIFunctionFactory.Create(services.GetRequiredService<AdminEventTools>().ReadRunEvents, "read_run_events"),
        ];
        return [.. reads.Select(tool => new BoundedResultTool(new MaskedResultTool(tool, masker), chars))];
    }

    public ValueTask DisposeAsync() => _scope?.DisposeAsync() ?? ValueTask.CompletedTask;
}
