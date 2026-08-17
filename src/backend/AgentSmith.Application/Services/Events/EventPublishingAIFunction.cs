using System.Diagnostics;
using System.Text.Json;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Events;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Events;

/// <summary>
/// Wraps an <see cref="AIFunction"/> so each tool invocation emits ToolCall +
/// ToolResult events. Payloads are METADATA ONLY — the args / result blobs stay out of
/// the event stream (same security class as prompts). The event carries tool name, arg
/// length, ok/fail and result length, nothing more.
/// <para>
/// p0423: it also carries the five measures — how long the call took, how much went in,
/// how much the tool produced before the bound cut it, how much reached the model, and
/// which attempt this was.
/// </para>
/// </summary>
public sealed class EventPublishingAIFunction(
    AIFunction inner,
    IEventPublisher eventPublisher,
    IRunContextAccessor runContext,
    ResultBoundReporter? resultBound = null) : AIFunction
{
    public override string Name => inner.Name;
    public override string Description => inner.Description;
    public override JsonElement JsonSchema => inner.JsonSchema;
    public override JsonSerializerOptions JsonSerializerOptions => inner.JsonSerializerOptions;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var runId = runContext.CurrentRunId;
        var scope = runContext.CurrentCallScope;
        var (argsLength, argsJson) = ToolArgumentFacts.Serialize(arguments, JsonSerializerOptions);
        // p0361: occurrence number of this exact (tool, args) invocation within the
        // current skill call. >=2 means the agent is redoing work — the measurable form
        // of "it read that file n times". Only the count leaves the process.
        var repeat = scope?.RegisterToolCall(inner.Name, ToolArgumentFacts.Hash(argsJson)) ?? 1;
        await PublishCallAsync(runId, scope, argsLength, arguments, repeat, cancellationToken);

        var ok = false;
        object? result = null;
        string? errorMessage = null;
        var sw = Stopwatch.StartNew();
        using var bound = resultBound?.Begin();
        try
        {
            result = await inner.InvokeAsync(arguments, cancellationToken);
            ok = true;
            return result;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            throw;
        }
        finally
        {
            sw.Stop();
            var delivered = ToolArgumentFacts.ResultLength(result, JsonSerializerOptions);
            await PublishResultAsync(
                runId, scope, ok, delivered, errorMessage, argsLength,
                bound?.OriginalChars ?? 0, sw.ElapsedMilliseconds, repeat);
        }
    }

    private async Task PublishCallAsync(
        string? runId, CallScope? scope, int argsLength,
        AIFunctionArguments arguments, int repeat, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(runId)) return;
        await eventPublisher.PublishAsync(
            new ToolCallEvent(
                runId, inner.Name, argsLength, DateTimeOffset.UtcNow,
                ToolArgumentFacts.Summarize(arguments),
                scope?.Role, scope?.Phase, scope?.RepoName,
                // p0222: the agent's one-sentence intent for this turn, captured from
                // the assistant text by EventPublishingChatClient onto the shared scope.
                scope?.Intent, repeat),
            cancellationToken);
    }

    private async Task PublishResultAsync(
        string? runId, CallScope? scope, bool ok, int delivered, string? errorMessage,
        int argsLength, long unbounded, long durationMs, int attempt)
    {
        if (string.IsNullOrEmpty(runId)) return;
        await eventPublisher.PublishAsync(
            new ToolResultEvent(
                runId, inner.Name, ok, delivered, DateTimeOffset.UtcNow, errorMessage,
                scope?.Role, scope?.Phase, scope?.RepoName,
                argsLength, unbounded, durationMs, attempt),
            CancellationToken.None);
    }
}
