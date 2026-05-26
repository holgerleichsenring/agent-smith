namespace AgentSmith.Server.Api.Models;

/// <summary>One file inside a run directory — surfaced in the dashboard sidebar.</summary>
public sealed record RunArtefactResponse(
    string Filename,
    long SizeBytes,
    string ContentType);
