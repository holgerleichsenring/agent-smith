namespace AgentSmith.Contracts.Models.Design;

/// <summary>2026-10-01-7f7ab: why a Figma read produced no body — never the body itself.</summary>
public enum FigmaReadFailureKind
{
    NotFound,
    Forbidden,
    RateLimited,
    Unreachable,
    Unknown,
}
