using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>2026-11-24-6811c: how the base-merge master ends its pass — the merge is whole, or it cannot be made whole
/// here, with the reason a person reads. A pass that never concludes counts as cannot.</summary>
public sealed class BaseMergeConcludeTool
{
    public bool? Whole { get; private set; }

    public string? Reason { get; private set; }

    public AIFunction GetTool() => AIFunctionFactory.Create(Conclude, "conclude");

    [Description("Ends your pass: whole=true when the merge keeps both sides' intent and the conflict or the red stage is resolved; whole=false when it cannot be resolved on this branch, with the reason a person needs.")]
    public string Conclude(
        [Description("True when the merge is whole.")] bool whole,
        [Description("One or two sentences: what you resolved, or why it cannot be resolved here.")] string reason)
    {
        Whole = whole;
        Reason = reason;
        return "Recorded.";
    }
}
