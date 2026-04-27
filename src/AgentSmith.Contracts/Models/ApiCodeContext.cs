namespace AgentSmith.Contracts.Models;

/// <summary>
/// Code-aware context for the api-security-scan pipeline. Built when
/// --source-path is provided. Skills consult these mappings to attach
/// file:line evidence to findings.
/// </summary>
public sealed record ApiCodeContext(
    IReadOnlyList<RouteHandlerLocation> RoutesToHandlers,
    IReadOnlyList<SourceFileExcerpt> AuthBootstrapFiles,
    IReadOnlyList<SourceFileExcerpt> SecurityMiddlewareRegistrations,
    IReadOnlyList<SourceFileExcerpt> UploadHandlers,
    double MappingConfidence);
