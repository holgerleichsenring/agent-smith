using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0315b: hands the master's final reply back to the spec-dialog turn
/// runner. The pipeline context never leaves ExecutePipelineUseCase, so the
/// server seeds a mutable <see cref="SpecDialogReplySlot"/> and this final
/// step copies ContextKeys.MasterAnswer into it.
/// </summary>
public sealed class CollectSpecDialogReplyHandler : ICommandHandler<CollectSpecDialogReplyContext>
{
    public Task<CommandResult> ExecuteAsync(
        CollectSpecDialogReplyContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<SpecDialogReplySlot>(ContextKeys.SpecDialogReplySlot, out var slot)
            || slot is null)
            return Task.FromResult(CommandResult.Fail(
                "Spec-dialog run has no reply slot — the turn runner must seed "
                + $"ContextKeys.{nameof(ContextKeys.SpecDialogReplySlot)}."));

        var answer = context.Pipeline.TryGet<string>(ContextKeys.MasterAnswer, out var a) ? a : null;
        if (string.IsNullOrWhiteSpace(answer))
            return Task.FromResult(CommandResult.Fail(
                "The design-partner master produced no reply text."));

        slot.Reply = answer;
        return Task.FromResult(CommandResult.Ok("Spec-dialog reply collected"));
    }
}
