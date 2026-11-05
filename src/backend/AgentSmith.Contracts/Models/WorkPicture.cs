namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-11-05-862ea: what the design partner said the operator's work is — the kind of undertaking, where its truth
/// lives (which upload, which repositories are scaffolding), and its expected size. Stated by the model through
/// state_work_picture, stored on the conversation, and replaced when the partner records a correction.
/// </summary>
public sealed record WorkPicture(UndertakingKind Kind, string Truth, UndertakingSize Size);
