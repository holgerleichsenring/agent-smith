namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-13-50c7: one normalized directory a context owns — its workdir, or one of the sources
/// its component is built and tested from.
/// </summary>
public sealed record StructureClaim(string Context, string Directory, bool IsWorkdir);
