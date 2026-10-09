namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>2026-10-09-86e1: one file of an uploaded set as the page lists it — never its bytes.</summary>
public sealed record ReferenceSetFileEntry(string Path, long Bytes, string MediaType);
