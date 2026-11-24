namespace AgentSmith.Server.Services.Pocs.Lifetime;

/// <summary>2026-11-23-c81ee: POST /api/pocs/{pocId}/acts/extend — the run that extended the PoC, its new expiry and its
/// extension count.</summary>
internal sealed record PocExtendAnswer(long PocId, string RunId, DateTimeOffset ExpiresAt, int Extensions);
