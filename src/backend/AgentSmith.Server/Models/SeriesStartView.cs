namespace AgentSmith.Server.Models;

/// <summary>2026-11-26-5f6e: what a person's start of a series ticket came to — started, queued or refused — with the
/// sentence of why and the run id when one exists.</summary>
public sealed record SeriesStartView(string Kind, string Text, string? RunId);
