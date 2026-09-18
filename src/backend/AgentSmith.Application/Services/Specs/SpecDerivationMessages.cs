using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-15-ffa7: the messages the deriver puts to the model — the opening exchange and
/// the rejection it answers. The pinned prompt is rendered by the deriver, which holds the catalog. Split from <see cref="SpecSetDeriver"/>, which was at the
/// file-length ceiling when its review gained a look of its own.
/// </summary>
internal static class SpecDerivationMessages
{
    /// <summary>The rendered judgement prompt, then the ticket with the look it may take.</summary>
    public static List<ChatMessage> Opening(
        string systemPrompt, Ticket ticket, IReadOnlyList<TicketSegment> segments,
        SpecSet? previous, string cause, PipelineContext pipeline, DerivationLook? look) =>
    [
        new(ChatRole.System, systemPrompt),
        new(ChatRole.User,
            SpecPromptComposer.Compose(ticket, segments, previous, cause, pipeline)
            + DerivationLookPromptSection.Render(look)),
    ];

    /// <summary>A rejection, in the shape the deriver already knows how to answer.</summary>
    public static ChatMessage Again(string verdict, string? error) =>
        new(ChatRole.User, $"{verdict}:\n{error}\nRespond again with ONLY the corrected JSON object.");
}
