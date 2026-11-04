namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-11-04-96a0b: one upload the turn that produced an outcome had open, by the address it was read by and the set
/// behind it. A value on the outcome because the turn's sandboxes are gone by the time the turn is kept.
/// </summary>
public sealed record UploadProvenance(string Address, string SetId);
