namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// p0315b: discriminated union of spec-draft validation outcomes for one
/// design-partner reply (mirrors the ScopeResolution union style).
/// </summary>
public abstract record SpecDraftOutcome;

/// <summary>The reply carries no phase-spec draft — a plain grounded answer.</summary>
public sealed record SpecDraftAbsent : SpecDraftOutcome;

/// <summary>The reply's fenced yaml block validates against the phase-spec schema.</summary>
public sealed record SpecDraftValid(string Yaml) : SpecDraftOutcome;

/// <summary>The reply's draft failed validation; Error names exactly what.</summary>
public sealed record SpecDraftInvalid(string Error) : SpecDraftOutcome;
