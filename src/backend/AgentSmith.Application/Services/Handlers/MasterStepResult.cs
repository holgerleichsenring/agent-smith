using AgentSmith.Application.Services.Loop.Sanity;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-17-a196a: the line a finished master step reports — the files it changed and, when there were
/// any, the writes it was refused because they would have landed in a repository its spec does not name.
/// </summary>
public static class MasterStepResult
{
    public static CommandResult Completed(PipelineContext pipeline, string skill, int filesChanged)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var line = $"Master '{skill}' completed: {filesChanged} files changed";
        var refused = RefusedWriteLog.Of(pipeline).Refused;
        return CommandResult.Ok(refused.Count == 0
            ? line
            : $"{line}; {refused.Count} write(s) refused outside this spec's repositories:\n- "
              + string.Join("\n- ", refused));
    }
}
