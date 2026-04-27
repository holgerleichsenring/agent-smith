namespace AgentSmith.Contracts.Models;

/// <summary>
/// A swagger route mapped to its handler location in source.
/// Confidence is 0..1 — values below 0.5 should not yield findings.
/// </summary>
public sealed record RouteHandlerLocation(
    string Method,
    string Path,
    string File,
    int StartLine,
    int EndLine,
    string HandlerSnippet,
    string Framework,
    double Confidence);
