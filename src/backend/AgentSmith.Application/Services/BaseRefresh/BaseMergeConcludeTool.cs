using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>2026-11-24-6811c: how the base-merge master ends its pass, with the reason a person reads. 2026-11-24-9dc0a:
/// the server decides whether the merge is whole; whole=false stops at once. In a conflict pass whole=true asks the
/// server's check first (<paramref name="check"/>: null when whole, else what is open), and while paths are open it answers
/// them instead of recording, so the master carries on in the same loop.</summary>
public sealed class BaseMergeConcludeTool(Func<CancellationToken, Task<string?>>? check = null)
{
    public bool? Whole { get; private set; }

    public string? Reason { get; private set; }

    public AIFunction GetTool() => AIFunctionFactory.Create(ConcludeAsync, "conclude");

    [Description("Ends your pass with the reason a person reads. whole=true when every conflicted path is resolved and staged, or the red stage is fixed — the server checks the tree and builds it; whole=false when it cannot be resolved on this branch, which stops the merge.")]
    public async Task<string> ConcludeAsync(
        [Description("True when the merge is whole; false stops it.")] bool whole,
        [Description("One or two sentences: what you resolved, or why it cannot be resolved here.")] string reason,
        CancellationToken cancellationToken = default)
    {
        if (whole && check is not null && await check(cancellationToken) is { } open)
            return $"Not whole yet — still open: {open}. Resolve and stage these, then conclude again.";
        Whole = whole;
        Reason = reason;
        return "Recorded.";
    }
}
