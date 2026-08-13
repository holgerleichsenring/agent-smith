using AgentSmith.Contracts.Models.Workers;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0416: turns one in-flight model call into the <see cref="WorkerRequest"/> an external
/// worker answers. The composition is deliberately total — messages, tool definitions with
/// their schemas, sampling options, run/step identity — because the worker stands exactly
/// where the provider stands and a reduced request would test a different system.
/// </summary>
public sealed class WorkerRequestComposer(WorkerMessageMapper messages, WorkerOptionsMapper options)
{
    public const string Protocol = "agentsmith.worker/1";

    public WorkerRequest Compose(
        IEnumerable<ChatMessage> chat, ChatOptions? chatOptions, WorkerCallIdentity identity) =>
        new(Protocol,
            RequestId: Guid.NewGuid().ToString("N")[..12],
            identity.RunId,
            identity.StepIndex,
            identity.Role,
            identity.Phase,
            identity.Repo,
            identity.AgentType,
            identity.Model,
            DateTimeOffset.UtcNow,
            messages.Map(chat),
            MapTools(chatOptions),
            options.Map(chatOptions));

    private static IReadOnlyList<WorkerToolDefinition> MapTools(ChatOptions? options) =>
        options?.Tools is not { Count: > 0 } tools
            ? []
            : [.. tools.Select(tool => new WorkerToolDefinition(
                tool.Name, tool.Description,
                tool is AIFunction function ? function.JsonSchema : null))];
}
