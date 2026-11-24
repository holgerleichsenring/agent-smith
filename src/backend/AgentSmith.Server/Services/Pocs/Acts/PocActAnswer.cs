namespace AgentSmith.Server.Services.Pocs.Acts;

/// <summary>2026-11-23-c81ed: the act run a request queued.</summary>
internal sealed record PocActAnswer(long PocId, string Act, string RunId);
