using AgentSmith.Infrastructure.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.ToolImages;

/// <summary>
/// 2026-10-01-283dd: the tool loop, carrying deposited images. What
/// <see cref="CreateResponseMessages"/> returns the base class adds to the history it keeps,
/// ONCE — so an image placed there is sent on every later iteration as part of the cached
/// prefix, never re-rendered and never appended to a per-call copy the way the governor's
/// reminder is. Each GetResponseAsync opens its own frame, which is what keeps a sub-agent's
/// images out of its parent's loop.
/// </summary>
public sealed class ToolImageFunctionInvokingChatClient(
    IChatClient inner, IToolImageRelay relay, ToolImageDelivery delivery)
    : FunctionInvokingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var loop = relay.OpenLoop();
        return await base.GetResponseAsync(messages, options, cancellationToken);
    }

    protected override IList<ChatMessage> CreateResponseMessages(
        ReadOnlySpan<FunctionInvocationResult> results) =>
        relay.Follow(base.CreateResponseMessages(results), delivery);
}
