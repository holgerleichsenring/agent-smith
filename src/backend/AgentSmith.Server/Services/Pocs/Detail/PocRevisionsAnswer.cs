namespace AgentSmith.Server.Services.Pocs.Detail;

/// <summary>2026-11-23-c81ed: GET /api/pocs/{pocId}/revisions — the PoC's revisions, newest first.</summary>
internal sealed record PocRevisionsAnswer(IReadOnlyList<PocRevisionRow> Revisions);
