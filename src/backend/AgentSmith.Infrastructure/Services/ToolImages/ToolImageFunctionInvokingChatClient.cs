using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.ToolImages;

/// <summary>
/// 2026-10-01-283dd: the tool loop, carrying deposited images. What
/// <see cref="CreateResponseMessages"/> returns the base class adds to the history it keeps,
/// ONCE — so an image placed there is sent on every later iteration as part of the cached
/// prefix, never re-rendered and never appended to a per-call copy the way the governor's
/// reminder is. Each GetResponseAsync opens its own frame, which is what keeps a sub-agent's
/// images out of its parent's loop.
/// <para>
/// 2026-10-07-6b9da: every tool result is bounded here, before it enters that history — the one
/// place every tool-bearing client shares. A single unbounded read_file of a lockfile pushed a
/// design turn past its window; a per-surface bound had missed that surface.
/// </para>
/// </summary>
public sealed class ToolImageFunctionInvokingChatClient(
    IChatClient inner, IToolImageRelay relay, ToolImageDelivery delivery, ILoggerFactory loggerFactory)
    : FunctionInvokingChatClient(inner)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<ToolImageFunctionInvokingChatClient>();

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var loop = relay.OpenLoop();
        return await base.GetResponseAsync(messages, options, cancellationToken);
    }

    protected override async ValueTask<object?> InvokeFunctionAsync(
        FunctionInvocationContext context, CancellationToken cancellationToken)
    {
        var result = await base.InvokeFunctionAsync(context, cancellationToken);
        var bounded = ToolResultBound.Apply(result);
        if (ToolResultBound.TextOf(result) is { } text && bounded is string kept && kept.Length < text.Length)
            _logger.LogInformation(
                "Tool result of {Tool} bounded: {Total} characters, {Kept} kept",
                context.Function.Name, text.Length, kept.Length);
        return bounded;
    }

    protected override IList<ChatMessage> CreateResponseMessages(
        ReadOnlySpan<FunctionInvocationResult> results) =>
        relay.Follow(base.CreateResponseMessages(results), delivery);
}
