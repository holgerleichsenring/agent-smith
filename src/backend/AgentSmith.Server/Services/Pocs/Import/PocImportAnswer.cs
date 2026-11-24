namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>2026-11-23-933db: a started import — the PoC, the revision and the poc-deploy run taking it in.</summary>
internal sealed record PocImportAnswer(long PocId, int Revision, string RunId);
