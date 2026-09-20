namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-09-20-3af8: the images of one design conversation as a turn sees them — how many the
/// conversation holds, and the most recent ones the turn may carry.
/// <para>
/// The two numbers are separate because the prompt says both: a turn carries the most recent
/// few, and an operator whose early diagram dropped out is told it exists rather than left to
/// assume it was read. <see cref="Recent"/> is already bounded by the loader, so a conversation
/// with twenty screenshots does not read twenty of them to send four.
/// </para>
/// </summary>
public sealed record DialogImageSet(int Existing, IReadOnlyList<DialogImage> Recent)
{
    /// <summary>A conversation with no images, and every turn that is not a design turn.</summary>
    public static readonly DialogImageSet None = new(0, []);
}
