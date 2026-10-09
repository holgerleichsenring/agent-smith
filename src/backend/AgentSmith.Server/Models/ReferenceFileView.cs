namespace AgentSmith.Server.Models;

/// <summary>2026-10-09-86e1: one file of an uploaded set as the Uploads tab lists it — its path and size.</summary>
public sealed record ReferenceFileView(string Path, long Bytes);
