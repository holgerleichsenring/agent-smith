using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Discriminated union of active-scope resolution outcomes at spec-dialog
/// session start (mirrors the ProjectResolverResult union style).
/// </summary>
public abstract record ScopeResolution;

/// <summary>The scope is unambiguous: explicit pick or single-project default.</summary>
public sealed record ScopeResolved(ActiveScope Scope) : ScopeResolution;

/// <summary>Multiple projects configured and none picked — the user must choose.</summary>
public sealed record ScopeChoiceRequired(IReadOnlyList<string> Projects) : ScopeResolution;

/// <summary>The explicitly picked project does not exist in the catalog.</summary>
public sealed record ScopeUnknownProject(string Requested, IReadOnlyList<string> Projects) : ScopeResolution;
