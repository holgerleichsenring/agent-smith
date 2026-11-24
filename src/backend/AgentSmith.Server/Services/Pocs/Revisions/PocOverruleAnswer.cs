namespace AgentSmith.Server.Services.Pocs.Revisions;

/// <summary>2026-11-23-933dc: the overrule run that was queued.</summary>
internal sealed record PocOverruleAnswer(long PocId, int Revision, string RunId);
