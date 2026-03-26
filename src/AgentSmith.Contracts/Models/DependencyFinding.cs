namespace AgentSmith.Contracts.Models;

/// <summary>
/// A vulnerability found in a project dependency.
/// </summary>
public sealed record DependencyFinding(
    string Package,
    string Version,
    string Severity,
    string? Cve,
    string Title,
    string Description,
    string? FixVersion,
    string Ecosystem);
