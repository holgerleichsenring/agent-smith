using AgentSmith.Contracts.Models.Workers;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// 2026-11-17-166ae: splits a <see cref="WorkerRequest"/> into what stays fixed across a conversation's calls and what
/// does not. Only the LEADING system messages go to the head — compaction pins them byte for byte — and the system
/// messages precede the tools, so a tool list that changes mid-loop still leaves the system prompt a cached prefix.
/// The input part is the request itself with its leading system messages and its tools moved to the head (its tool
/// list is empty there), so every field still travels and the identity fields keep their one definition.
/// </summary>
public static class WorkerPromptSections
{
    public const string SystemRole = "system";

    public static (Head Head, WorkerRequest Input) Split(WorkerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var leading = request.Messages.TakeWhile(m => m.Role == SystemRole).ToList();
        return (new Head(request.Protocol, request.AgentType, request.Model, leading, request.Tools),
            request with { Messages = [.. request.Messages.Skip(leading.Count)], Tools = [] });
    }

    /// <summary>The part every call of a conversation shares.</summary>
    public sealed record Head(
        string Protocol, string AgentType, string Model,
        IReadOnlyList<WorkerMessage> System, IReadOnlyList<WorkerToolDefinition> Tools);
}
