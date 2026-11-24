using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Application.Services.Pocs.Variables;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-23-933dg: the poc-variables run's one step. The seed leaves the context as soon as it is read, so no later
/// step, checkpoint or failure report holds a value; a PoC that is not the run's project's is refused; the rules are
/// checked again against what the PoC holds; then secrets go to the store under the PoC's scope and plain values onto
/// the Poc row. Who may write was decided by the route that started the run. No reload follows: the configuration load
/// holds no PoC value, and every run of the project reads its values when it starts. Names and kinds only, never a value.
/// </summary>
public sealed class WritePocVariablesHandler(
    IPocVariableStore store, ISecretVault vault, PocVariableRules rules, PocVariableWriter writer, PocsConfigSource config,
    ILogger<WritePocVariablesHandler> logger) : ICommandHandler<WritePocVariablesContext>
{
    public async Task<CommandResult> ExecuteAsync(WritePocVariablesContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<PocVariableWrite>(ContextKeys.PocVariableWrite, out var write) || write is null)
            return CommandResult.Fail("A poc-variables run has no write — only the PoC variables route and the import start one.");
        pipeline.Remove(ContextKeys.PocVariableWrite);
        if (await store.ReadAsync(write.PocId, cancellationToken) is not { } poc
            || !string.Equals(poc.Project, context.Project.Name, StringComparison.OrdinalIgnoreCase))
            return Report(pipeline, CommandResult.Fail($"PoC {write.PocId} is no PoC of project '{context.Project.Name}'; nothing was written."));
        var existing = (await vault.StatesAsync(SecretScope.Poc(poc.PocId), cancellationToken)).Select(s => s.Name).Concat(poc.Plain.Select(p => p.Name));
        if (rules.Problems(write.Entries, existing, config.Current().Variables) is { Count: > 0 } problems)
            return Report(pipeline, CommandResult.Fail(string.Join(" ", problems)));
        var runId = pipeline.TryGet<string>(ContextKeys.RunId, out var id) ? id ?? string.Empty : string.Empty;
        var (plain, refusal) = await writer.WriteAsync(write, poc.Plain, runId, cancellationToken);
        await store.WritePlainAsync(poc.PocId, PocPlainVariables.ToJson(plain), cancellationToken);
        logger.LogInformation("PoC {PocId} variables written in run {RunId}: {Entries}", poc.PocId, runId, string.Join(", ", write.Entries));
        return Report(pipeline, refusal is null
            ? CommandResult.Ok($"The variables of '{poc.Name}' were written by {write.Person} in run {runId}: "
                + $"{string.Join(", ", write.Entries)}. They apply at the PoC's next deploy.")
            : CommandResult.Fail(refusal));
    }

    private static CommandResult Report(PipelineContext pipeline, CommandResult result)
    {
        if (pipeline.TryGet<ActSlot<string>>(ContextKeys.PocVariableReport, out var slot)) slot?.Set(result.Message);
        return result;
    }
}
