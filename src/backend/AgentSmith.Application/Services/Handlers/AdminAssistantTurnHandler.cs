using AgentSmith.Domain.Models;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Admin;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Turns;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-f3a8a: one turn of the admin assistant. Its master is embedded in the server, its tools are the
/// server's (<see cref="IAdminToolSurface"/>), and it continues its thread like a design turn; the reply and what the
/// turn proposes go to CollectSpecDialogReply, which hands them to the conversation.
/// </summary>
public sealed class AdminAssistantTurnHandler(
    IAgenticLoopRunner loopRunner, IPromptCatalog prompts, IAdminToolSurface surface,
    AdminTurnTools turnTools, IAdminTurnOutcome outcome,
    ITurnRunRecorderAccessor? runs = null) : ICommandHandler<AdminAssistantTurnContext>
{
    public async Task<CommandResult> ExecuteAsync(AdminAssistantTurnContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<SpecDialogReplySlot>(ContextKeys.SpecDialogReplySlot, out var slot) || slot is null)
            return CommandResult.Fail("An admin turn has no reply slot — the admin turn runner must seed it.");
        await LinkRunAsync(pipeline, slot, cancellationToken);
        var request = AdminTurnRequest.Build(context.Project.Agent, prompts.Get(PipelinePresets.AdminAssistantMaster),
            AdminTurnPrompt.Build(pipeline), turnTools.For(pipeline, surface.Tools(pipeline)), pipeline, slot);
        var result = await loopRunner.RunAsync(request, cancellationToken);
        PipelineCostTracker.GetOrCreate(pipeline).Track(result.Response);
        pipeline.Set(ContextKeys.MasterAnswer, result.Response.Text ?? string.Empty);
        pipeline.Set(ContextKeys.MasterSkillName, PipelinePresets.AdminAssistantMaster);
        pipeline.Set(ContextKeys.SpecDialogOutcome, outcome.Of(pipeline));
        return CommandResult.Ok("Admin assistant answered");
    }

    // The turn's run id on its reply slot and on the conversation's row before the first call, as the design turn's grounding does.
    private async Task LinkRunAsync(PipelineContext pipeline, SpecDialogReplySlot slot, CancellationToken cancellationToken)
    {
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId)) return;
        slot.RunId = runId;
        await (runs?.Current?.RecordAsync(runId, cancellationToken) ?? Task.CompletedTask);
    }
}
