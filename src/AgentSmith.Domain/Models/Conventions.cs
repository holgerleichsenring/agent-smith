namespace AgentSmith.Domain.Models;

/// <summary>
/// Free-form convention strings derived by the analyzer from observed code
/// (e.g. naming patterns, test layout, error-handling style). Empty when
/// the analyzer cannot infer them with confidence — never guessed.
/// </summary>
public sealed record Conventions(
    string? NamingPattern,
    string? TestLayout,
    string? ErrorHandling);
