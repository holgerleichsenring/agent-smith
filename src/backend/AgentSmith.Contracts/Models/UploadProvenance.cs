namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-11-04-96a0b: one upload the turn that produced an outcome had open, by the address it was read by and the set
/// behind it. A value on the outcome because the turn's sandboxes are gone by the time the turn is kept.
/// </summary>
/// <param name="ThisTurn">2026-11-04-96a0c: the readable files this turn opened with read_file.</param>
/// <param name="Opened">The readable files the conversation has opened, this turn included.</param>
/// <param name="Files">The files read_file can open.</param>
public sealed record UploadProvenance(string Address, string SetId, int ThisTurn = 0, int Opened = 0, int Files = 0);
