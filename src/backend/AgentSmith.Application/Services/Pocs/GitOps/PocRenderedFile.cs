namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: one file the render puts into the PoC's deploy folder — its name in the folder and its
/// content.</summary>
public sealed record PocRenderedFile(string Name, string Content);
