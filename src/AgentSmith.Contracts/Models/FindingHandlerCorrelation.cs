namespace AgentSmith.Contracts.Models;

/// <summary>
/// Links an external scanner finding (Nuclei / ZAP) to the source-code handler
/// that owns the affected endpoint, when the URL+method match a mapped route at
/// confidence ≥ 0.5. Handler is null when no route matches — the correlation
/// row still carries the finding identity so downstream consumers can reason
/// about coverage.
/// </summary>
public sealed record FindingHandlerCorrelation(
    string FindingSource,   // "nuclei" | "zap"
    string FindingId,       // NucleiFinding.TemplateId or ZapFinding.AlertRef
    string Severity,
    string Method,
    string Url,
    RouteHandlerLocation? Handler);
