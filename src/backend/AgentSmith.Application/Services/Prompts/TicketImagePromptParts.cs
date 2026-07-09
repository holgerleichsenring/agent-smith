using AgentSmith.Contracts.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// p0317: turns downloaded ticket image attachments into M.E.AI image content
/// parts for the master's user message, capped so a screenshot-heavy ticket
/// cannot flood the context window. Only called for vision-capable models
/// (<c>agent.supports_vision</c>); non-vision models get a prompt note instead.
/// </summary>
public static class TicketImagePromptParts
{
    /// <summary>Maximum image parts attached to one user message.</summary>
    public const int MaxImages = 10;

    public static IReadOnlyList<AIContent> Build(IReadOnlyList<TicketImageAttachment>? images) =>
        images is null or { Count: 0 }
            ? []
            : images
                .Take(MaxImages)
                .Select(i => (AIContent)new DataContent(i.Content, i.MediaType))
                .ToList();
}
