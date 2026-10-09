namespace AgentSmith.Application.Models;

/// <summary>2026-10-08-e8b9j: where an upload's files are read from — its conversation and its set.</summary>
public sealed record ReferenceUploadAddress(string Session, string SetId);
