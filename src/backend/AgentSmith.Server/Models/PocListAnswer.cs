namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-933da: GET /api/pocs — the PoCs the caller sees, whether the Import button is theirs, and the
/// targets for the target strip (a repo ref holds no secret, so every pocs.read holder sees them).</summary>
public sealed record PocListAnswer(IReadOnlyList<PocListItem> Pocs, bool MayImport, IReadOnlyList<PocTargetView> Targets);
