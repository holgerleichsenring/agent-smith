namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-10-08-e8b9k: one image the approval cites, as a run carries it.
/// </summary>
/// <param name="SetId">The id it is stored and cited under.</param>
/// <param name="Path">Its file inside the carrying repository, outside the commit; null when this
/// process could not read it — a note, not a failure: an image is shown, not built against.</param>
/// <param name="Shown">Whether it rides the master's message as a picture (vision, within the ceiling).</param>
public sealed record CarriedReferenceImage(string SetId, string? Path, bool Shown);
