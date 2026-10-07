using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// 2026-08-27-3eb1: ends a tool loop that is about to overrun the role's stated input
/// window. It sits BELOW UseFunctionInvocation and INSIDE <see cref="CompactingChatClient"/>,
/// so it measures the view that is actually forwarded — reduction gets first refusal and
/// this fires only when reduction was not enough or is not configured.
///
/// <para>The exit is the one the iteration budget already takes: the conversation keeps
/// its evidence, one instruction demands the final answer, and tool calling is switched
/// off for that turn. A shallow answer from partial evidence beats an HTTP 400. The
/// caller's <see cref="ChatOptions"/> is CLONED, never mutated: the function-invoking
/// client hands the same instance down on every iteration.</para>
/// </summary>
internal sealed class ContextPressureFinalizingChatClient(
    IChatClient inner, int windowTokens, string role, ILogger? logger = null)
    : DelegatingChatClient(inner)
{
    // Deliberately below the 0.7 compaction trigger's headroom and below 1.0: the
    // estimator counts message text only — never options.Tools, whose JSON schemas are
    // a four-figure token count on the scout surface — so a measured 0.85 of the window
    // is already more than 0.85 of the real payload. The remainder also has to hold the
    // response the finalize turn is about to generate.
    private const double HardBoundRatio = 0.85;

    internal const string FinalizeInstruction =
        "The conversation has reached the input limit of the model running it. Stop "
        + "calling tools and reply now with your final answer, in the exact format your "
        + "instructions demand, based on the evidence you have already gathered. "
        + "Omit anything you found no evidence for. No prose about the limit.";

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var list = messages as IList<ChatMessage> ?? messages.ToList();
        var estimated = CompactingChatClient.EstimateTokens(list);
        if (options?.Tools is not { Count: > 0 } || estimated < (int)(windowTokens * HardBoundRatio))
            return await base.GetResponseAsync(list, options, cancellationToken);

        logger?.LogWarning(
            "Context pressure on {Role}: ~{Estimated} of {Window} window tokens — finalising "
            + "the tool loop instead of overflowing it", role, estimated, windowTokens);
        var forced = new List<ChatMessage>(list) { new(ChatRole.User, FinalizeInstruction) };
        return await base.GetResponseAsync(Fitted(forced), FinalizeOptions(options!), cancellationToken);
    }

    // 2026-10-07-6b9dc: the instruction alone shrinks nothing, so a recent tail that outgrew
    // the window was refused anyway. The forwarded copy's tool results are re-cut to the same
    // bound. What still sits between the bound and the window is forwarded as before — it may
    // well be answered; only a request at or above the window, which the provider would
    // refuse, is not sent.
    private IList<ChatMessage> Fitted(IList<ChatMessage> forced)
    {
        var target = (int)(windowTokens * HardBoundRatio);
        var fitted = ForwardedViewFit.Fit(forced, target);
        var estimated = CompactingChatClient.EstimateTokens(fitted);
        if (estimated >= windowTokens)
            throw new InvalidOperationException(Unfittable(estimated, ForwardedViewFit.ToolResultTokens(fitted)));
        if (!ReferenceEquals(fitted, forced))
            logger?.LogInformation(
                "Re-cut tool results for {Role}: forwarding ~{Estimated} of {Window} window tokens",
                role, estimated, windowTokens);
        return fitted;
    }

    // Thrown above ContextLengthRefusalChatClient, which never sees it — so it names the
    // role and the window itself.
    private string Unfittable(int estimated, int toolTokens) =>
        $"The '{role}' model role cannot finish within its stated window of {windowTokens} tokens: "
        + $"with every tool result cut to {ForwardedViewFit.FloorChars} characters the request still "
        + $"estimates ~{estimated} tokens, {estimated - toolTokens} of them not tool output (instructions, "
        + "ticket and conversation). Cutting tool output cannot make it fit; the role needs a larger "
        + "window (models.<role>.context_window_tokens, if the deployment allows it) or a smaller prompt.";

    // Tools stay declared — a provider rejects a request whose history carries tool
    // blocks without the tools parameter — but ToolMode=None forbids further calls.
    private static ChatOptions FinalizeOptions(ChatOptions options)
    {
        var clone = options.Clone();
        clone.ToolMode = ChatToolMode.None;
        return clone;
    }
}
