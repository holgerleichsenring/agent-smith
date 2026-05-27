using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// Aggregated outcome of a parallel batch run. Slots are indexed in skill-graph order.
/// FirstFailure surfaces the earliest-in-graph failed slot (used for fail-fast reporting).
/// FirstInsertNext returns the earliest-in-graph slot whose CommandResult.InsertNext is set.
/// </summary>
public sealed class BatchOutcome
{
    private readonly IReadOnlyList<BatchSlot> _slots;
    private readonly IReadOnlyList<LinkedListNode<PipelineCommand>> _nodes;
    private readonly int _firstStepIndex;

    public BatchOutcome(
        IReadOnlyList<BatchSlot> slots,
        IReadOnlyList<LinkedListNode<PipelineCommand>> nodes,
        int firstStepIndex)
    {
        _slots = slots;
        _nodes = nodes;
        _firstStepIndex = firstStepIndex;
    }

    public IReadOnlyList<BatchSlot> Slots => _slots;
    public IReadOnlyList<LinkedListNode<PipelineCommand>> Nodes => _nodes;
    public int FirstStepIndex => _firstStepIndex;

    public BatchSlot? FirstFailure() =>
        _slots.FirstOrDefault(s => s is not null && !s.Result.IsSuccess);

    public (LinkedListNode<PipelineCommand> Node, CommandResult Result)? FirstInsertNext()
    {
        for (var i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (slot is null) continue;
            if (slot.Result.InsertNext is { Count: > 0 }) return (_nodes[i], slot.Result);
        }
        return null;
    }
}
