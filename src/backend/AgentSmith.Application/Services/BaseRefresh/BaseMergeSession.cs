using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Tools;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>2026-11-24-9dc0a: one base-merge master conversation — its situation, the one MasterConversation every pass
/// appends to, and the one FilesystemToolHost whose writes accumulate across the passes.</summary>
public sealed class BaseMergeSession(BaseMergeSituation situation, FilesystemToolHost files)
{
    public BaseMergeSituation Situation { get; } = situation;

    public MasterConversation Conversation { get; } = new();

    /// <summary>The paths the file tools wrote so far, each once.</summary>
    public IReadOnlyList<string> WrotePaths() => [.. files.GetChanges().Select(c => c.Path.Value).Distinct(StringComparer.Ordinal)];

    /// <summary>The file tools and the pass's conclude.</summary>
    public IList<AITool> Tools(BaseMergeConcludeTool conclude)
    {
        ArgumentNullException.ThrowIfNull(conclude);
        return [.. files.GetTools(null, null).Select(BoundedResultTool.Wrap), conclude.GetTool()];
    }

    /// <summary>What the pass that ended with <paramref name="conclude"/> answers.</summary>
    public BaseMergePass Pass(BaseMergeConcludeTool conclude)
    {
        ArgumentNullException.ThrowIfNull(conclude);
        return new BaseMergePass(conclude.Whole, conclude.Reason, files.GetChanges()) { Session = this };
    }
}
