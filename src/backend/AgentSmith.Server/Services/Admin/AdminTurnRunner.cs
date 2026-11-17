using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Admin;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Services.Admin;

/// <summary>
/// 2026-11-17-f3a8a: runs ONE turn of an admin conversation as an admin-turn pipeline run on the installation. It
/// keeps the design turn's conversation machinery — the gate, the activity line, the run link, the thread written as
/// it grows, the messages sent mid-turn and the endings that carry the run id — and opens no sandbox, because the
/// turn reads the installation through its tools. An admin agent that is not configured ends the turn with a notice
/// naming dialogue.admin_agent.
/// </summary>
public sealed class AdminTurnRunner(
    IConfigurationLoader configLoader, ServerContext serverContext, ExecutePipelineUseCase pipelineUseCase,
    SpecDialogTurnGate gate, DashboardActivityChannel activity, SpecDialogThreadStore threads,
    SpecDialogTurnEnding ending, SpecDialogTurnRunLink runLink, SpecDialogThreadWriteLink threadWrites,
    SpecDialogOperatorInbox inbox) : ISpecDialogTurnRunner
{
    private static readonly IReadOnlyDictionary<string, ISourceScopeSandbox> NoTemplates =
        new Dictionary<string, ISourceScopeSandbox>();

    public async Task<SpecDialogTurnResult?> RunTurnAsync(
        ConversationState state, TurnPoster poster, CancellationToken cancellationToken)
    {
        var slot = new SpecDialogReplySlot();
        try
        {
            return await RunAsync(state, poster, slot, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await ending.CancelledAsync(state, slot.RunId);
            throw;
        }
        catch (Exception ex)
        {
            await ending.ThrownAsync(state, ex, slot.RunId);
            return null;
        }
    }

    private async Task<SpecDialogTurnResult> RunAsync(
        ConversationState state, TurnPoster poster, SpecDialogReplySlot slot, CancellationToken cancellationToken)
    {
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        var agent = InstallationProject.AdminAgent(config);
        var seeds = AdminTurnSeeds.Build(state, slot, agent, await threads.LoadAsync(state, agent, cancellationToken),
            poster, inbox.For(state.JobId, poster, slot.Thread));
        var request = new PipelineRequest(
            ProjectName: InstallationScope.Name, PipelineName: PipelinePresets.AdminTurnName, Headless: true, Context: seeds);
        using var linking = runLink.Observe(state);
        var writing = threadWrites.Observe(state, agent, slot.Thread);
        var computing = gate.Begin(state.JobId);
        try
        {
            using var reporting = activity.Observe(state, computing);
            var result = await pipelineUseCase.ExecuteAsync(request, serverContext.ConfigPath, cancellationToken);
            return await ending.OfAsync(state, slot, InstallationProject.Build(config, PipelinePresets.AdminTurnName),
                NoTemplates, result, cancellationToken);
        }
        finally
        {
            await writing.CloseAsync();
            gate.Finish(state.JobId, computing);
        }
    }
}
