using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// 2026-09-20-3af8: the images one design turn carries into the master call, and the two
/// numbers the prompt says out loud — how many the conversation holds, and how many ride this
/// message.
/// <para>
/// THE CEILING IS FOUR, NOT THE TICKET PATH'S TEN, and it is not inherited because the two
/// bound different things. Ten bounds a ticket read once in a run; a conversation re-sends its
/// whole prompt every turn, on a surface with neither a cost fence nor an iteration ceiling, so
/// whatever number is chosen is multiplied by the length of the conversation rather than paid
/// once. Four is what a person attaches to one question.
/// </para>
/// <para>
/// WHICH four is a decision, not a discovery: the most recent, because the operator attached
/// them to the message they are asking about — and the transcript is rendered flat, so nothing
/// ties an image to a turn anyway.
/// </para>
/// </summary>
public sealed record DialogImageParts(IReadOnlyList<AIContent> Parts, int Existing, int Carried)
{
    /// <summary>Maximum image parts one design turn attaches to its user message.</summary>
    public const int MaxImages = 4;

    /// <summary>Every master call that is not a design turn.</summary>
    public static readonly DialogImageParts None = new([], 0, 0);

    /// <summary>
    /// What this turn carries. A model that cannot see images carries none and says so through
    /// the numbers — the conversation's screenshot is never dropped in silence.
    /// </summary>
    public static DialogImageParts From(AgenticMasterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Pipeline.TryGet<DialogImageSet>(ContextKeys.SpecDialogImages, out var set)
            || set is null || set.Existing == 0)
            return None;

        // TakeLast, not Take: the set arrives oldest-first, so the most recent are at the END
        // of it. The loader bounds its read by the same ceiling, and this still decides WHICH
        // of a longer list is carried rather than trusting that it was bounded.
        var carried = context.AgentConfig.SupportsVision ? set.Recent.TakeLast(MaxImages).ToList() : [];
        return new(
            [.. carried.Select(image => (AIContent)new DataContent(image.Content, image.MediaType))],
            set.Existing,
            carried.Count);
    }
}
