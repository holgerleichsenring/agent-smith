using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0341f: how a user turn is put on the wire, in ONE place.
/// <para>
/// p0317 attaches ticket images as content parts beside the prompt text. The runner
/// composed that message inline, and once the conversation started being carried across
/// passes (p0341f) a second composition appeared in the handler — which promptly dropped
/// the images, because a message rebuilt from the prompt STRING has only the prose. The
/// pipeline harness caught it; a helper both callers share is what stops it recurring.
/// </para>
/// </summary>
internal static class LoopUserMessage
{
    internal static ChatMessage Compose(string prompt, IReadOnlyList<AIContent>? imageParts) =>
        imageParts is { Count: > 0 } images
            ? new ChatMessage(ChatRole.User, [new TextContent(prompt), .. images])
            : new ChatMessage(ChatRole.User, prompt);
}
