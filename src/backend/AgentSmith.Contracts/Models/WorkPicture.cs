namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-11-05-862ea: what the design partner said the operator's work is — the kind of undertaking, where its truth
/// lives (where what is wanted is described, and what the repositories already provide), and its expected size. Stated by the model through
/// state_work_picture, stored on the conversation, and replaced when the partner records a correction.
/// </summary>
/// <param name="Effort">2026-11-10-548f: what the review of the conversation's drafts may spend, as a cost-cap tier;
/// Unknown when none was stated.</param>
public sealed record WorkPicture(UndertakingKind Kind, string Truth, UndertakingSize Size,
    Configuration.ComplexityTier Effort = Configuration.ComplexityTier.Unknown);
